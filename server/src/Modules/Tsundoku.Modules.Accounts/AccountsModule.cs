using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Persistence;
using Tsundoku.Common.Events;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Features.OnAlreadyRegisteredNoticeRequested;
using Tsundoku.Modules.Accounts.Features.OnConfirmationRequested;
using Tsundoku.Modules.Accounts.Features.ConfirmEmail;
using Tsundoku.Modules.Accounts.Features.DeleteAccount;
using Tsundoku.Modules.Accounts.Features.GetMe;
using Tsundoku.Modules.Accounts.Features.GoogleSignIn;
using Tsundoku.Modules.Accounts.Features.SignOutEverywhere;
using Tsundoku.Modules.Accounts.Features.UpdateSettings;
using Tsundoku.Modules.Accounts.Features.RefreshSession;
using Tsundoku.Modules.Accounts.Features.ResendConfirmation;
using Tsundoku.Modules.Accounts.Features.SignIn;
using Tsundoku.Modules.Accounts.Features.SignUp;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts;

/// <summary>
/// The accounts module: users, Identity bearer tokens, sessions and settings. This is the only public type of the module.
/// The access token is the Identity bearer token, and it is the default authentication scheme of the host.
/// </summary>
public static class AccountsModule
{
    internal const string Schema = "accounts";

    public static IHostApplicationBuilder AddAccountsModule(this IHostApplicationBuilder builder)
    {
        builder.AddModuleDatabase<AccountsDbContext>(Schema);
        var services = builder.Services;

        services.AddOptions<AccountsOptions>().BindConfiguration(AccountsOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AccountsOptions>, AccountsOptionsValidator>();

        services.AddIdentityCore<AccountUser>()
            .AddEntityFrameworkStores<AccountsDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        // Configured from the validated options when they are first read, not from a copy of the configuration made here.
        services.AddOptions<IdentityOptions>().Configure<IOptions<AccountsOptions>>((identity, accounts) =>
        {
            var options = accounts.Value;
            identity.User.RequireUniqueEmail = true;
            identity.SignIn.RequireConfirmedEmail = true;
            // Length only: composition rules make people pick worse passwords.
            identity.Password.RequiredLength = options.PasswordMinLength;
            identity.Password.RequireDigit = false;
            identity.Password.RequireLowercase = false;
            identity.Password.RequireUppercase = false;
            identity.Password.RequireNonAlphanumeric = false;
            identity.Lockout.MaxFailedAccessAttempts = options.LockoutMaxFailedAttempts;
            identity.Lockout.DefaultLockoutTimeSpan = options.LockoutDuration;
            identity.ClaimsIdentity.UserIdClaimType = UserClaims.Subject;
        });
        services.AddOptions<DataProtectionTokenProviderOptions>().Configure<IOptions<AccountsOptions>>(
            (token, accounts) => token.TokenLifespan = accounts.Value.EmailTokenLifetime);

        services.AddAuthentication(IdentityConstants.BearerScheme).AddBearerToken(IdentityConstants.BearerScheme);
        services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme).Configure<IOptions<AccountsOptions>>((bearer, accounts) =>
        {
            bearer.BearerTokenExpiration = accounts.Value.AccessTokenLifetime;
            bearer.RefreshTokenExpiration = accounts.Value.RefreshTokenLifetime;
        });

        // The keys that protect the tokens live in the database, so every replica and every restart reads the same ones.
        services.AddDataProtection().SetApplicationName("tsundoku").PersistKeysToDbContext<AccountsDbContext>();

        // The mailer port. Both registrations are one object, so a replacement must replace both.
        services.AddSingleton<LogEmailSender>();
        services.AddSingleton<IAccountMailer>(provider => provider.GetRequiredService<LogEmailSender>());
        services.AddSingleton<IEmailSender<AccountUser>>(provider => provider.GetRequiredService<IAccountMailer>());
        services.AddScoped<ConfirmationMail>();
        services.AddDomainEventHandler<ConfirmationRequested, ConfirmationRequestedHandler>();
        services.AddDomainEventHandler<AlreadyRegisteredNoticeRequested, AlreadyRegisteredNoticeRequestedHandler>();
        services.AddScoped<CredentialTiming>();

        services.AddOptions<GoogleOptions>().BindConfiguration(GoogleOptions.Section)
            // An unset env var such as GOOGLE_CLIENT_ID binds as [""]: blank entries mean "not configured", so Google stays off.
            .PostConfigure(google => google.ClientIds = [.. google.ClientIds.Where(id => !string.IsNullOrWhiteSpace(id))])
            .ValidateDataAnnotations()
            .Validate(google => google.MetadataAddress is { IsAbsoluteUri: true, Scheme: "https" },
                $"{GoogleOptions.Section}:{nameof(GoogleOptions.MetadataAddress)} must be an absolute https URL.")
            .ValidateOnStart();
        // The client gets the host's standard resilience (timeout, retries, circuit breaker); the manager caches the keys and refreshes them.
        services.AddHttpClient(GoogleIdTokenVerifier.HttpClientName)
            .ConfigureHttpClient((provider, client) => client.Timeout = provider.GetRequiredService<IOptions<GoogleOptions>>().Value.HttpTimeout);
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(provider =>
        {
            var google = provider.GetRequiredService<IOptions<GoogleOptions>>().Value;
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(GoogleIdTokenVerifier.HttpClientName);
            return new ConfigurationManager<OpenIdConnectConfiguration>(
                google.MetadataAddress.AbsoluteUri, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever(client) { RequireHttps = true })
            {
                AutomaticRefreshInterval = google.KeysAutomaticRefreshInterval,
                RefreshInterval = google.KeysRefreshInterval,
            };
        });
        services.AddSingleton<GoogleIdTokenVerifier>();

        services.AddValidator<SignUpCommand, SignUpCommandValidator>();
        services.AddValidator<ResendConfirmationCommand, ResendConfirmationCommandValidator>();
        services.AddValidator<SignInCommand, SignInCommandValidator>();
        // Anonymous: these touch only tables without an owner policy.
        services.AddAnonymousCommand<SignUpCommand, Success, SignUpHandler, AccountsDbContext>();
        services.AddAnonymousCommand<ConfirmEmailCommand, Success, ConfirmEmailHandler, AccountsDbContext>();
        services.AddAnonymousCommand<ResendConfirmationCommand, Success, ResendConfirmationHandler, AccountsDbContext>();
        services.AddAnonymousCommand<SignInCommand, SignInOutcome, SignInHandler, AccountsDbContext>();
        services.AddAnonymousCommand<RefreshSessionCommand, RefreshOutcome, RefreshSessionHandler, AccountsDbContext>();
        services.AddAnonymousCommand<GoogleSignInCommand, SignedIn, GoogleSignInHandler, AccountsDbContext>();

        services.AddValidator<UpdateSettingsCommand, UpdateSettingsCommandValidator>();
        services.AddQuery<GetMeQuery, MeResponse, GetMeHandler, AccountsDbContext>();
        services.AddCommand<UpdateSettingsCommand, SettingsResponse, UpdateSettingsHandler, AccountsDbContext>();
        services.AddCommand<SignOutEverywhereCommand, Success, SignOutEverywhereHandler, AccountsDbContext>();
        services.AddCommand<DeleteAccountCommand, DeleteAccountOutcome, DeleteAccountHandler, AccountsDbContext>();
        return builder;
    }

    /// <summary>Maps the endpoints of the module under <paramref name="api"/>.</summary>
    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");
        SignUpEndpoint.Map(auth);
        ConfirmEmailEndpoint.Map(auth);
        ResendConfirmationEndpoint.Map(auth);
        SignInEndpoint.Map(auth);
        RefreshSessionEndpoint.Map(auth);
        GoogleSignInEndpoint.Map(auth);

        var me = api.MapGroup("/me").WithTags("Me");
        GetMeEndpoint.Map(me);
        UpdateSettingsEndpoint.Map(me);
        SignOutEverywhereEndpoint.Map(me);
        DeleteAccountEndpoint.Map(me);
        return api;
    }

    public static Task MigrateAccountsAsync(this IHost host) => host.MigrateAsync<AccountsDbContext>();
}

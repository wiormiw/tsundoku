using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Persistence;
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
        return builder;
    }

    public static Task MigrateAccountsAsync(this IHost host) => host.MigrateAsync<AccountsDbContext>();
}

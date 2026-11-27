using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>Sign-up, sign-in and refresh through the real endpoints.</summary>
internal static class AccountsClient
{
    public const string Password = "correct horse battery staple";

    public static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    /// <summary>Signs up a new address and follows the confirmation link that the outbox mails.</summary>
    public static async Task<(string Email, string Password)> SignUpConfirmedAsync(ApiFactory factory, CancellationToken cancellationToken)
    {
        using var client = factory.CreateApiClient(user: null);
        var email = await SignUpAsync(client, cancellationToken);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        string? link;
        while ((link = factory.Mail.To(email).FirstOrDefault(mail => mail.Link is not null)?.Link) is null)
        {
            if (DateTimeOffset.UtcNow > deadline) throw new TimeoutException("The outbox did not send the confirmation mail.");
            await Task.Delay(50, cancellationToken);
        }
        (await client.GetAsync(new Uri(link).PathAndQuery, cancellationToken)).EnsureSuccessStatusCode();
        return (email, Password);
    }

    /// <summary>Signs up a new address and leaves it unconfirmed.</summary>
    public static async Task<string> SignUpAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var email = NewEmail();
        (await client.PostAsJsonAsync("/api/auth/sign-up", new { email, password = Password }, cancellationToken)).EnsureSuccessStatusCode();
        return email;
    }

    public static Task<HttpResponseMessage> SignIn(HttpClient client, string email, string password, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("/api/auth/sign-in", new { email, password }, cancellationToken);

    public static async Task<AccessTokenResponse> SignInAsync(HttpClient client, string email, string password, CancellationToken cancellationToken)
    {
        using var response = await SignIn(client, email, password, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken))!;
    }

    public static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, cancellationToken);

    public static async Task<HttpResponseMessage> GetLists(HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/todo-lists");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request, cancellationToken);
    }

    /// <summary>The body with its traceId masked: the id differs on every request by design and says nothing about the refusal.</summary>
    public static async Task<string> ProblemBody(HttpResponseMessage response, CancellationToken cancellationToken) =>
        Regex.Replace(await response.Content.ReadAsStringAsync(cancellationToken), "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");
}

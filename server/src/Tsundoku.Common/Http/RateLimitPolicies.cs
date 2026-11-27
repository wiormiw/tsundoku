namespace Tsundoku.Common.Http;

/// <summary>The names for <c>RequireRateLimiting</c>. The Valkey limiter reads them; ASP.NET's own limiter is not running.</summary>
public static class RateLimitPolicies
{
    /// <summary>The strict bucket for endpoints that take credentials.</summary>
    public const string Credentials = "credentials";
}

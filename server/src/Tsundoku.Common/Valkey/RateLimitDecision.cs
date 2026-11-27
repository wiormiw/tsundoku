namespace Tsundoku.Common.Valkey;

/// <summary>Whether a request may pass and, when not, how long until a token is due.</summary>
internal readonly record struct RateLimitDecision(bool Allowed, TimeSpan RetryAfter);

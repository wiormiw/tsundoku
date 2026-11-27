using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Identity;

/// <summary>The user of the current request, from the validated token. Never from the request body (tip 25.08).</summary>
public interface ICurrentUser
{
    /// <summary>It throws when there is no user, because a use case that needs a user cannot continue.</summary>
    UserId Id { get; }
}

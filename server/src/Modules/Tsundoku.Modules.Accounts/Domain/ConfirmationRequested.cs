using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>A user needs a confirmation mail. Only the id: the handler makes the token, so no secret is stored in the outbox.</summary>
internal sealed record ConfirmationRequested(Guid UserId) : IDomainEvent;

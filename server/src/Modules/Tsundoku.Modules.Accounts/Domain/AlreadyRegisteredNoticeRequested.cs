using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>Someone signed up with the address of this user. The mail says so, after the commit, so both sign-up branches cost the same.</summary>
internal sealed record AlreadyRegisteredNoticeRequested(Guid UserId) : IDomainEvent;

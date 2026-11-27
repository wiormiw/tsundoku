using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Runs a command in one transaction of its user (row-level security sees only that user's rows) and saves its changes only when it
/// succeeds. The save also writes the outbox rows. After a refusal nothing is saved, also when the handler already changed an aggregate
/// in memory. A null user is for the anonymous commands, which touch only tables without an owner policy.
/// </summary>
internal static class UnitOfWorkDecorator
{
    internal sealed class CommandHandler<TCommand, TResult, TDbContext>(ICommandHandler<TCommand, TResult> inner, TDbContext db, UserId? user)
        : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull where TDbContext : DbContext
    {
        public async Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            // A throw disposes the transaction, which rolls it back.
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, user, cancellationToken);
            var result = await inner.Handle(command, cancellationToken);
            if (result is Error)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new ConcurrencyException(exception);
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Saves the changes of a command in one transaction, only when the command succeeds. The save also writes the outbox rows.
/// After a refusal nothing is saved, also when the handler already changed an aggregate in memory.
/// </summary>
internal static class UnitOfWorkDecorator
{
    internal sealed class CommandHandler<TCommand, TResult, TDbContext>(ICommandHandler<TCommand, TResult> inner, TDbContext db)
        : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull where TDbContext : DbContext
    {
        public async Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var result = await inner.Handle(command, cancellationToken);
            if (result is Error) return result;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                throw new ConcurrencyException(exception);
            }
            return result;
        }
    }
}

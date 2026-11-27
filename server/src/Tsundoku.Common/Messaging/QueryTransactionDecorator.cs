using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Runs a query in a transaction of its user, so row-level security applies to reads too. It never saves:
/// the commit only ends the transaction, which clears the user setting.
/// </summary>
internal static class QueryTransactionDecorator
{
    internal sealed class QueryHandler<TQuery, TResult, TDbContext>(IQueryHandler<TQuery, TResult> inner, TDbContext db, UserId user)
        : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult> where TResult : notnull where TDbContext : DbContext
    {
        public async Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, user, cancellationToken);
            var result = await inner.Handle(query, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
    }
}

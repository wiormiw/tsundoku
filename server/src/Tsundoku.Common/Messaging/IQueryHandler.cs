using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Common.Messaging;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult> where TResult : notnull
{
    Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken);
}

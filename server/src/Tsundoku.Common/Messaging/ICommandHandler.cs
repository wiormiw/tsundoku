using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Common.Messaging;

public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull
{
    Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken);
}

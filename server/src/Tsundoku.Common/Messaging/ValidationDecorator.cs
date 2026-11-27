using System.Text.Json;
using FluentValidation;
using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// Runs the validator of the request, when it has one, before the handler. A request that is not valid never reaches the handler.
/// The checks here give clients clear field messages. The value objects of the domain still protect the domain.
/// </summary>
internal static class ValidationDecorator
{
    internal sealed class CommandHandler<TCommand, TResult>(ICommandHandler<TCommand, TResult> inner, IValidator<TCommand>? validator)
        : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull
    {
        public async Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken) =>
            await Check(validator, command, cancellationToken) is { } invalid ? invalid : await inner.Handle(command, cancellationToken);
    }

    internal sealed class QueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> inner, IValidator<TQuery>? validator)
        : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult> where TResult : notnull
    {
        public async Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken) =>
            await Check(validator, query, cancellationToken) is { } invalid ? invalid : await inner.Handle(query, cancellationToken);
    }

    private static async Task<ValidationError?> Check<T>(IValidator<T>? validator, T request, CancellationToken cancellationToken)
    {
        if (validator is null) return null;
        var result = await validator.ValidateAsync(request, cancellationToken);
        return result.IsValid ? null : new ValidationError(
            result.Errors
                .GroupBy(failure => JsonKey(failure.PropertyName))
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()));
    }

    /// <summary>The keys are the JSON names that the client sent (<c>title</c>, not <c>Title</c>).</summary>
    private static string JsonKey(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}

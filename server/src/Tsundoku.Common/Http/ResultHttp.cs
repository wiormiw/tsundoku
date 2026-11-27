using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Common.Http;

/// <summary>
/// The one mapping from use-case results to HTTP. <see cref="Error"/> is a closed hierarchy, so the switch in
/// <see cref="ToProblem"/> has no default arm: a new kind of error is a compile error here until it has a status.
/// Each problem has a stable <c>code</c> extension that clients can check (tip 26.04).
/// </summary>
public static class ResultHttp
{
    public static ProblemHttpResult ToProblem(this Error error) => error switch
    {
        ValidationError invalid => TypedResults.Problem(new HttpValidationProblemDetails(invalid.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = invalid.Message,
            Extensions = { ["code"] = invalid.Code },
        }),
        NotFoundError notFound => Problem(StatusCodes.Status404NotFound, "Not found.", notFound),
        UnauthorizedError unauthorized => Problem(StatusCodes.Status401Unauthorized, "Unauthorized.", unauthorized),
        ForbiddenError forbidden => Problem(StatusCodes.Status403Forbidden, "Forbidden.", forbidden),
        ConflictError conflict => Problem(StatusCodes.Status409Conflict, "Conflict.", conflict),
    };

    public static Results<Ok<T>, ProblemHttpResult> ToOk<T>(this Result<T> result) where T : notnull => result switch
    {
        T value => TypedResults.Ok(value),
        Error error => error.ToProblem(),
    };

    public static Results<NoContent, ProblemHttpResult> ToNoContent(this Result<Success> result) => result switch
    {
        Success => TypedResults.NoContent(),
        Error error => error.ToProblem(),
    };

    public static Results<Created<T>, ProblemHttpResult> ToCreated<T>(this Result<T> result, Func<T, string> location) where T : notnull =>
        result switch
        {
            T value => TypedResults.Created(location(value), value),
            Error error => error.ToProblem(),
        };

    private static ProblemHttpResult Problem(int status, string title, Error error) => TypedResults.Problem(
        statusCode: status, title: title, detail: error.Message, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Api.Http;

/// <summary>
/// The one place where unexpected exceptions become HTTP responses (tip 7.06).
/// Expected refusals do not come here: the use cases return them as <c>Result</c> values.
/// This handler converts a broken invariant (409), a lost update (409), a body that it cannot read (400),
/// and a client that disconnected (499). All other exceptions go to the 500 response of the framework,
/// which shows no exception details outside Development.
/// </summary>
internal sealed partial class ProblemDetailsExceptionHandler(IProblemDetailsService problems, ILogger<ProblemDetailsExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected or the request timeout ended it. Nginx uses 499 for "client closed request".
            httpContext.Response.StatusCode = 499;
            return true;
        }

        (ProblemDetails Problem, string Code)? mapped = exception switch
        {
            BadHttpRequestException { InnerException: JsonException json } => (new ValidationProblemDetails(
                new Dictionary<string, string[]> { [JsonPath(json)] = ["The JSON is malformed, has the wrong type here, or has an unknown property."] })
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request body could not be read.",
            }, "bad_request"),
            BadHttpRequestException bad => (new ProblemDetails { Status = bad.StatusCode, Title = "Bad request.", Detail = bad.Message }, "bad_request"),
            ConcurrencyException conflict => (new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = conflict.Message + " Read it again, then retry.",
            }, "concurrency_conflict"),
            DomainException domain => (new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict.", Detail = domain.Message }, domain.Code),
            _ => null,
        };
        if (mapped is not { } result) return false;

        if (exception is DomainException invariant) InvariantBroken(logger, invariant, invariant.Code);
        result.Problem.Extensions["code"] = result.Code;
        httpContext.Response.StatusCode = result.Problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = result.Problem, Exception = exception });
    }

    /// <summary>Changes <c>$.title</c> to <c>title</c>, the key that the form of the client uses.</summary>
    private static string JsonPath(JsonException json) =>
        json.Path is { Length: > 2 } path && path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : "body";

    [LoggerMessage(EventId = 200, Level = LogLevel.Warning, Message = "Domain invariant {ErrorCode} was broken")]
    private static partial void InvariantBroken(ILogger logger, Exception exception, string errorCode);
}

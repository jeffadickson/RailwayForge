using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace RailwayForge.AspNetCore;

/// <summary>
/// Optional interface custom error types can implement for automatic RFC 9457 ProblemDetails conversion.
/// </summary>
public interface IHttpError : IForgeError
{
    int StatusCode { get; }
    string Title { get; }
    IDictionary<string, object?>? Extensions => null;
}

/// <summary>
/// Minimal API and RFC 9457 ProblemDetails adapters for <see cref="Result{T, TError}"/>,
/// <see cref="Result{TError}"/>, and <see cref="ForgeError"/>.
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Exhaustive C# 15 switch mapping closed <see cref="ForgeError"/> cases to RFC 9457 ProblemDetails.
    /// </summary>
    public static IResult ToProblemDetails(this ForgeError error) => error switch
    {
        ForgeError.Validation e => HttpResults.Problem(
            title: "Validation Error",
            detail: e.Message,
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["field"] = e.Field }),
        ForgeError.NotFound e => HttpResults.Problem(
            title: "Not Found",
            detail: e.Message,
            statusCode: StatusCodes.Status404NotFound,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["resource"] = e.Resource, ["id"] = e.Id }),
        ForgeError.Conflict e => HttpResults.Problem(
            title: "Conflict",
            detail: e.Message,
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["target"] = e.Target }),
        ForgeError.Unauthorized e => HttpResults.Problem(
            title: "Unauthorized",
            detail: e.Message,
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code }),
        ForgeError.Forbidden e => HttpResults.Problem(
            title: "Forbidden",
            detail: e.Message,
            statusCode: StatusCodes.Status403Forbidden,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["operation"] = e.Operation }),
        ForgeError.Unavailable e => HttpResults.Problem(
            title: "Service Unavailable",
            detail: e.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["dependency"] = e.Dependency }),
        ForgeError.Unexpected e => HttpResults.Problem(
            title: "Internal Server Error",
            detail: e.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object?> { ["code"] = e.Code, ["exceptionType"] = e.ExceptionType }),
    };

    public static IResult ToProblemDetails(this IHttpError error)
    {
        var ext = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Extensions is not null)
        {
            foreach (var (k, v) in error.Extensions)
                ext[k] = v;
        }
        return HttpResults.Problem(
            title: error.Title,
            detail: error.Message,
            statusCode: error.StatusCode,
            extensions: ext);
    }

    public static IResult ToHttpResult<T>(
        this Result<T, ForgeError> result, Func<T, IResult>? onOk = null) =>
        result.Match(
            v => onOk is not null ? onOk(v) : HttpResults.Ok(v),
            e => e.ToProblemDetails());

    public static IResult ToHttpResult(
        this Result<ForgeError> result, Func<IResult>? onOk = null) =>
        result.Match(
            () => onOk is not null ? onOk() : HttpResults.NoContent(),
            e => e.ToProblemDetails());

    public static IResult ToHttpResult<T, TError>(
        this Result<T, TError> result,
        Func<TError, IResult> mapError,
        Func<T, IResult>? onOk = null) =>
        result.Match(
            v => onOk is not null ? onOk(v) : HttpResults.Ok(v),
            mapError);

    public static IResult ToHttpResult<T, TError>(
        this Result<T, TError> result,
        Func<T, IResult>? onOk = null)
        where TError : IHttpError =>
        result.Match(
            v => onOk is not null ? onOk(v) : HttpResults.Ok(v),
            e => e.ToProblemDetails());

    public static IResult ToCreatedHttpResult<T>(
        this Result<T, ForgeError> result, Func<T, string> uriFactory) =>
        result.Match(
            v => HttpResults.Created(uriFactory(v), v),
            e => e.ToProblemDetails());

    public static IResult ToHttpResult<T>(
        this Result<T, IReadOnlyList<ForgeError>> result, Func<T, IResult>? onOk = null) =>
        result.Match(
            v => onOk is not null ? onOk(v) : HttpResults.Ok(v),
            errors => HttpResults.Problem(
                title: "One or more validation errors occurred.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "VALIDATION_MULTIPLE",
                    ["errors"] = errors.Select(e => new { e.Code, e.Message }).ToArray(),
                }));
}

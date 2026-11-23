using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RailwayForge.OpenTelemetry;

/// <summary>
/// Built-in OpenTelemetry <see cref="Meter"/> and instrumentation helpers for RailwayForge operations.
/// Zero external dependencies: uses BCL <see cref="ActivitySource"/> and <see cref="Meter"/> directly.
/// </summary>
public static class RailwayTelemetry
{
    public const string MeterName = "RailwayForge";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> OperationsTotal =
        Meter.CreateCounter<long>("railway.results.total", "{operation}", "Total RailwayForge result operations.");

    public static readonly Counter<long> ErrorsTotal =
        Meter.CreateCounter<long>("railway.results.errors", "{error}", "Failed RailwayForge result operations.");

    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("railway.results.duration_ms", "ms", "Duration of traced RailwayForge operations.");

    /// <summary>Records the outcome of a <see cref="Result{T, TError}"/> on the active span and meter.</summary>
    public static Result<T, TError> RecordTelemetry<T, TError>(
        this Result<T, TError> result,
        string operationName,
        Activity? activity = null)
    {
        if (result.IsOk)
        {
            OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operationName),
                new KeyValuePair<string, object?>("outcome", "ok"));
            activity?.SetTag("railway.outcome", "ok");
            return result;
        }

        if (result.TryGetError(out var err))
        {
            string code = err is IForgeError fe ? fe.Code : typeof(TError).Name;
            string message = err is IForgeError fe2 ? fe2.Message : err?.ToString() ?? "error";

            OperationsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operationName),
                new KeyValuePair<string, object?>("outcome", "error"),
                new KeyValuePair<string, object?>("error.code", code));

            ErrorsTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operationName),
                new KeyValuePair<string, object?>("error.code", code));

            if (activity is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, message);
                activity.SetTag("railway.outcome", "error");
                activity.SetTag("error.code", code);
                activity.SetTag("error.type", err?.GetType().Name ?? typeof(TError).Name);
                activity.SetTag("error.message", message);
            }
        }
        return result;
    }

    /// <summary>Wraps a synchronous result-producing operation in an OpenTelemetry span and duration histogram.</summary>
    public static Result<T, TError> Trace<T, TError>(
        this ActivitySource source,
        string operationName,
        Func<Result<T, TError>> work)
    {
        using var activity = source.StartActivity(operationName, ActivityKind.Internal);
        long start = Stopwatch.GetTimestamp();
        var result = work();
        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        DurationMs.Record(elapsedMs, new KeyValuePair<string, object?>("operation", operationName));
        return result.RecordTelemetry(operationName, activity);
    }

    /// <summary>Wraps an async result-producing operation in an OpenTelemetry span and duration histogram.</summary>
    public static async Task<Result<T, TError>> TraceAsync<T, TError>(
        this ActivitySource source,
        string operationName,
        Func<Task<Result<T, TError>>> work)
    {
        using var activity = source.StartActivity(operationName, ActivityKind.Internal);
        long start = Stopwatch.GetTimestamp();
        var result = await work().ConfigureAwait(false);
        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        DurationMs.Record(elapsedMs, new KeyValuePair<string, object?>("operation", operationName));
        return result.RecordTelemetry(operationName, activity);
    }
}

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RailwayForge.OpenTelemetry;

/// <summary>
/// Built-in OpenTelemetry <see cref="Meter"/> and instrumentation helpers for RailwayForge operations.
/// Zero external dependencies: uses BCL <see cref="ActivitySource"/> and <see cref="Meter"/> directly.
/// </summary>
public static class RailwayTelemetry
{
    /// <summary>OpenTelemetry meter name emitted by RailwayForge (<c>"RailwayForge"</c>).</summary>
    public const string MeterName = "RailwayForge";

    /// <summary>Shared <see cref="System.Diagnostics.Metrics.Meter"/> publishing RailwayForge counters and latency histograms.</summary>
    public static readonly Meter Meter = new(MeterName, "1.1.0");

    /// <summary>Total count of recorded RailwayForge result operations tagged by <c>operation</c> and <c>outcome</c>.</summary>
    public static readonly Counter<long> OperationsTotal =
        Meter.CreateCounter<long>("railway.results.total", "{operation}", "Total RailwayForge result operations.");

    /// <summary>Count of failed RailwayForge result operations tagged by <c>operation</c> and <c>error.code</c>.</summary>
    public static readonly Counter<long> ErrorsTotal =
        Meter.CreateCounter<long>("railway.results.errors", "{error}", "Failed RailwayForge result operations.");

    /// <summary>Duration histogram in milliseconds for traced RailwayForge operations.</summary>
    public static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("railway.results.duration_ms", "ms", "Duration of traced RailwayForge operations.");

    private static void RecordOk(string operationName, Activity? activity)
    {
        OperationsTotal.Add(1,
            new KeyValuePair<string, object?>("operation", operationName),
            new KeyValuePair<string, object?>("outcome", "ok"));
        activity?.SetTag("railway.outcome", "ok");
    }

    private static void RecordError<TError>(TError err, string operationName, Activity? activity)
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

    /// <summary>Records the outcome of a <see cref="Result{T, TError}"/> on the active span and meter.</summary>
    public static Result<T, TError> RecordTelemetry<T, TError>(
        this Result<T, TError> result,
        string operationName,
        Activity? activity = null)
    {
        if (result.IsOk) RecordOk(operationName, activity);
        else if (result.TryGetError(out var err)) RecordError(err, operationName, activity);
        return result;
    }

    /// <summary>Records the outcome of a void-equivalent <see cref="Result{TError}"/> on the active span and meter.</summary>
    public static Result<TError> RecordTelemetry<TError>(
        this Result<TError> result,
        string operationName,
        Activity? activity = null)
    {
        if (result.IsOk) RecordOk(operationName, activity);
        else if (result.TryGetError(out var err)) RecordError(err, operationName, activity);
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

    /// <summary>Wraps a synchronous void-equivalent result operation in an OpenTelemetry span and duration histogram.</summary>
    public static Result<TError> Trace<TError>(
        this ActivitySource source,
        string operationName,
        Func<Result<TError>> work)
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

    /// <summary>Wraps an async void-equivalent result operation in an OpenTelemetry span and duration histogram.</summary>
    public static async Task<Result<TError>> TraceAsync<TError>(
        this ActivitySource source,
        string operationName,
        Func<Task<Result<TError>>> work)
    {
        using var activity = source.StartActivity(operationName, ActivityKind.Internal);
        long start = Stopwatch.GetTimestamp();
        var result = await work().ConfigureAwait(false);
        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        DurationMs.Record(elapsedMs, new KeyValuePair<string, object?>("operation", operationName));
        return result.RecordTelemetry(operationName, activity);
    }
}

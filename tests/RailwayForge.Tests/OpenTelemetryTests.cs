using System.Diagnostics;
using System.Diagnostics.Metrics;
using RailwayForge.OpenTelemetry;

namespace RailwayForge.Tests;

public sealed class OpenTelemetryTests
{
    [Fact]
    public async Task Trace_And_TraceAsync_Record_Span_Status_Tags_And_Metrics()
    {
        using var source = new ActivitySource("RailwayForge.Tests.Source");
        List<Activity> captured = [];
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => captured.Add(a),
        };
        ActivitySource.AddActivityListener(activityListener);

        long errorCount = 0;
        long totalCount = 0;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (inst, listener) =>
        {
            if (inst.Meter.Name == RailwayTelemetry.MeterName)
                listener.EnableMeasurementEvents(inst);
        };
        meterListener.SetMeasurementEventCallback<long>((inst, val, _, _) =>
        {
            if (inst.Name == "railway.results.errors") Interlocked.Add(ref errorCount, val);
            if (inst.Name == "railway.results.total") Interlocked.Add(ref totalCount, val);
        });
        meterListener.Start();

        var ok = source.Trace<int, ForgeError>("op.ok", () => 123);
        Assert.True(ok.IsOk);

        var fail = await source.TraceAsync<int, ForgeError>(
            "op.fail",
            () => Task.FromResult<Result<int, ForgeError>>(new ForgeError.Conflict("Order", "duplicate")));
        Assert.True(fail.IsError);

        Assert.Equal(2, captured.Count);
        Assert.Equal("ok", captured[0].GetTagItem("railway.outcome"));
        Assert.Equal(ActivityStatusCode.Error, captured[1].Status);
        Assert.Equal("CONFLICT", captured[1].GetTagItem("error.code"));
        Assert.True(totalCount >= 2);
        Assert.True(errorCount >= 1);
    }
}

using System.Diagnostics;
using RailwayForge;
using RailwayForge.OpenTelemetry;

namespace RailwayForge.Tests;

public sealed class SystemsAdoptionEnhancementTests
{
    private enum SimErr { InvalidWidth = 1, Timeout = 2, ParseFailed = 3, NullInput = 4 }

    [Fact]
    public void Combine_ThreeAndFourArgs_Reject_Uninitialized_Default_Results()
    {
#pragma warning disable RF0002
        Result<int, SimErr> uninit = default;
#pragma warning restore RF0002

        Assert.Throws<InvalidOperationException>(() =>
            Result.Combine<int, int, int, SimErr>(1, uninit, 3));
        Assert.Throws<InvalidOperationException>(() =>
            Result.Combine<int, int, int, SimErr>(1, 2, uninit));
        Assert.Throws<InvalidOperationException>(() =>
            Result.Combine<int, int, int, int, SimErr>(1, 2, 3, uninit));
        Assert.Throws<InvalidOperationException>(() =>
            Result.Combine<SimErr>(Result.Ok<SimErr>(), default));
    }

    [Fact]
    public void StatePassing_HotPath_Combinators_Allocate_Zero_Bytes()
    {
        static long Run(int iterations, int scale, int limit)
        {
            long acc = 0;
            for (int i = 1; i <= iterations; i++)
            {
                Result<int, SimErr> r = i;
                int v = r
                    .Map(scale, static (x, s) => x * s)
                    .Ensure(limit, static (x, lim) => x <= lim, static (_, _) => SimErr.InvalidWidth)
                    .Bind(scale, static (x, s) => Result.Ok<int, SimErr>(x + s))
                    .Match(1, static (x, bias) => x + bias, static (_, _) => 0);
                acc += v;
            }
            return acc;
        }

        _ = Run(1_000, 3, 2_000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long sum = Run(20_000, 3, 50_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(sum > 0);
        Assert.Equal(0L, allocated);
    }

    [Fact]
    public void FromCondition_FromNullable_FromTry_And_OrThrow_Bridge_Existing_Boundaries()
    {
        var okCond = Result.FromCondition(true, 42, SimErr.InvalidWidth);
        var badCond = Result.FromCondition(false, 42, SimErr.InvalidWidth);
        Assert.Equal(42, okCond.OrThrow(e => new InvalidOperationException(e.ToString())));
        Assert.Throws<InvalidOperationException>(() =>
            badCond.OrThrow(e => new InvalidOperationException(e.ToString())));

        string? text = "clk";
        string? missing = null;
        Assert.Equal("clk", Result.FromNullable(text, SimErr.NullInput).Recover("fallback"));
        Assert.True(Result.FromNullable(missing, SimErr.NullInput).TryGetError(out var nullErr) && nullErr == SimErr.NullInput);

        int? maybeNum = 7;
        int? noNum = null;
        Assert.Equal(7, Result.FromNullable(maybeNum, SimErr.NullInput).Recover(-1));
        Assert.True(Result.FromNullable(noNum, SimErr.NullInput).IsError);

        bool parsed = int.TryParse("123", out int n);
        var fromTryOk = Result.FromTry(parsed, n, SimErr.ParseFailed);
        Assert.Equal(123, fromTryOk.Recover(-1));

        bool failedParse = int.TryParse("xyz", out int bad);
        var fromTryErr = Result.FromTry(failedParse, bad, SimErr.ParseFailed);
        Assert.True(fromTryErr.TryGetError(out var pErr) && pErr == SimErr.ParseFailed);
    }

    [Fact]
    public async Task VoidResult_Combinators_Collect_Combine_And_ValueTask_Pipelines_Work()
    {
        int sideEffect = 0;
        var unitOk = Result.FromCondition(true, SimErr.Timeout)
            .Tap(() => sideEffect += 1)
            .Ensure(() => sideEffect == 1, SimErr.InvalidWidth);
        Assert.True(unitOk.IsOk);

        bool switchedOk = false;
        unitOk.Switch(() => switchedOk = true, _ => switchedOk = false);
        Assert.True(switchedOk);

        var recovered = Result.Fail(SimErr.Timeout).OrElse(_ => Result.Ok<string>());
        Assert.True(recovered.IsOk);

        var combinedErr = Result.Combine(Result.Fail(SimErr.Timeout), Result.Fail(SimErr.ParseFailed));
        Assert.Equal([SimErr.Timeout, SimErr.ParseFailed], combinedErr.Match(() => [], e => e));

        var collectedErr = Result.Collect([Result.Ok<SimErr>(), Result.Fail(SimErr.InvalidWidth), Result.Fail(SimErr.Timeout)]);
        Assert.Equal([SimErr.InvalidWidth, SimErr.Timeout], collectedErr.Match(() => [], e => e));

        var traversedOk = Result.Traverse<int, int, SimErr>([1, 2, 3], x => x * 10);
        Assert.Equal([10, 20, 30], traversedOk.Match(v => v, _ => []));

        var traversedFail = Result.Traverse<int, int, SimErr>([1, -2, 3], x => x > 0 ? x : SimErr.InvalidWidth);
        Assert.True(traversedFail.TryGetError(out var tErr) && tErr == SimErr.InvalidWidth);

        int tapped = 0;
        var vt = await Result.TryValueTaskAsync<int, SimErr>(
                () => ValueTask.FromResult(10),
                _ => SimErr.Timeout)
            .EnsureAsync(x => x > 0, _ => SimErr.InvalidWidth)
            .TapAsync(x => tapped = x)
            .MapErrorAsync(e => e.ToString());
        Assert.Equal(10, tapped);
        Assert.Equal(10, vt.Recover(-1));

        var vtErr = await Result.TryValueTaskAsync<SimErr>(
                () => ValueTask.FromException(new IOException("disk full")),
                _ => SimErr.Timeout)
            .MapErrorAsync(e => $"err:{e}");
        Assert.True(vtErr.TryGetError(out var errText) && errText == "err:Timeout");
    }

    [Fact]
    public void ValidationBag_Merge_And_VoidResult_Telemetry_Work()
    {
        var bag = new ValidationBag<SimErr>();
        bag.Merge(Result.Ok<SimErr>())
           .Merge(Result.Fail(SimErr.InvalidWidth))
           .Check(false, SimErr.ParseFailed);
        Assert.Equal(2, bag.Count);
        Assert.True(bag.Build().IsError);
        bag.Clear();
        Assert.False(bag.HasErrors);
        Assert.True(bag.Build().IsOk);

        using var source = new ActivitySource("RailwayForge.Tests.UnitTelemetry");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var ok = source.Trace("void.ok", () => Result.Ok<ForgeError>());
        var err = source.Trace("void.fail", () => Result.Fail<ForgeError>(new ForgeError.Validation("clk", "missing")));
        Assert.True(ok.IsOk);
        Assert.True(err.IsError);
    }
}

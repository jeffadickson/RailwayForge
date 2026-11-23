using System.Text.Json;
using RailwayForge;

namespace RailwayForge.Tests;

public sealed class CoreResultTests
{
    private enum DomainErr { Negative = 1, TooLarge = 2, Zero = 3 }

    [Fact]
    public void CSharp15_Union_ImplicitConversions_And_ExhaustiveSwitch_Work()
    {
        Result<int, DomainErr> ok = 42;
        Result<int, DomainErr> err = DomainErr.Negative;

        // Compiler-checked exhaustive switch (no wildcard arm required).
        int v1 = ok switch { int v => v, DomainErr => -1 };
        int v2 = err switch { int v => v, DomainErr e => -(int)e };

        Assert.Equal(42, v1);
        Assert.Equal(-1, v2);
        Assert.True(ok.IsOk);
        Assert.True(err.IsError);
        Assert.True(ok.HasValue);
    }

    [Fact]
    public void HotPath_ValueTypes_Allocate_Zero_Bytes()
    {
        static long RunHotLoop(int count)
        {
            long sum = 0;
            for (int i = 0; i < count; i++)
            {
                Result<int, DomainErr> r = (i & 3) == 0 ? DomainErr.Zero : i;
                sum += r switch { int v => v, DomainErr => 0 };
                sum += r.Map(static x => x * 2).Ensure(static x => x > 0, DomainErr.Negative).Recover(0);
            }
            return sum;
        }

        // Warmup JIT & cached static lambda delegates
        _ = RunHotLoop(1_000);

        long before = GC.GetAllocatedBytesForCurrentThread();
        long sum = RunHotLoop(50_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(sum > 0);
        Assert.Equal(0L, allocated);
    }

    [Fact]
    public void Functor_And_Monad_Laws_Hold()
    {
        Result<int, DomainErr> ok = 7;
        Result<int, DomainErr> err = DomainErr.Negative;

        // Functor identity & composition
        Assert.Equal(ok, ok.Map(x => x));
        Assert.Equal(err, err.Map(x => x));
        Assert.Equal(ok.Map(x => x + 3).Map(x => x * 2), ok.Map(x => (x + 3) * 2));

        // Monad left identity, right identity, associativity
        static Result<int, DomainErr> F(int x) => x > 0 ? x * 3 : DomainErr.Negative;
        static Result<int, DomainErr> G(int x) => x < 100 ? x + 5 : DomainErr.TooLarge;

        Assert.Equal(F(7), Result.Ok<int, DomainErr>(7).Bind(F));
        Assert.Equal(ok, ok.Bind(Result.Ok<int, DomainErr>));
        Assert.Equal(err, err.Bind(Result.Ok<int, DomainErr>));
        Assert.Equal(ok.Bind(F).Bind(G), ok.Bind(x => F(x).Bind(G)));
    }

    [Fact]
    public async Task Async_And_ValueTask_Railway_Combinators_Compose_Cleanly()
    {
        int tapped = 0;
        var res = await Task.FromResult<Result<int, DomainErr>>(10)
            .MapAsync(x => x * 2)
            .EnsureAsync(x => x > 5, _ => DomainErr.Negative)
            .TapAsync(x => tapped = x)
            .BindAsync(x => Task.FromResult<Result<string, DomainErr>>($"n={x}"));

        Assert.Equal(20, tapped);
        Assert.Equal("n=20", res.Match(v => v, e => e.ToString()));

        var vtRes = await new ValueTask<Result<int, DomainErr>>(5)
            .MapAsync(x => x + 1)
            .BindAsync(x => new ValueTask<Result<int, DomainErr>>(x * 10))
            .MatchAsync(v => v, _ => -1);
        Assert.Equal(60, vtRes);
    }

    [Fact]
    public async Task Combine_Collect_Partition_Traverse_And_ValidationBag_Work()
    {
        var cOk = Result.Combine<int, string, DomainErr>(1, "a");
        Assert.Equal((1, "a"), cOk.Match(v => v, _ => default));

        var cErr = Result.Combine<int, string, DomainErr>(DomainErr.Negative, DomainErr.TooLarge);
        Assert.Equal([DomainErr.Negative, DomainErr.TooLarge], cErr.Match(_ => [], errs => errs));

        var collected = Result.Collect<int, DomainErr>([1, DomainErr.Zero, 3, DomainErr.Negative]);
        Assert.Equal([DomainErr.Zero, DomainErr.Negative], collected.Match(_ => [], errs => errs));

        var (vals, errs) = Result.Partition<int, DomainErr>([1, DomainErr.Zero, 3]);
        Assert.Equal([1, 3], vals);
        Assert.Equal([DomainErr.Zero], errs);

        var traversed = await Result.TraverseAsync<int, int, DomainErr>(
            [1, 2, 3],
            (x, _) => Task.FromResult<Result<int, DomainErr>>(x * 10));
        Assert.Equal([10, 20, 30], traversed.Match(v => v, _ => []));

        var bag = new ValidationBag<ForgeError>();
        bag.Check(false, new ForgeError.Validation("email", "required"))
           .Check(true, new ForgeError.Validation("name", "ignored"))
           .Check(false, new ForgeError.Validation("age", "must be positive"));
        var built = bag.Build(() => "user");
        Assert.True(built.IsError);
        Assert.Equal(2, built.Match(_ => 0, list => list.Count));
    }

    [Fact]
    public async Task Try_And_TryAsync_Map_Exceptions_Without_Swallowing_Cancellation()
    {
        var r = Result.Try<int, ForgeError>(
            () => int.Parse("not-an-int"),
            ex => new ForgeError.Validation("input", ex.Message));
        Assert.True(r.IsError);
        Assert.True(r.TryGetError(out var err));
        Assert.IsType<ForgeError.Validation>(err);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Result.TryAsync<int, ForgeError>(
                async () => { await Task.Delay(10, cts.Token); return 1; },
                ex => new ForgeError.Unexpected(ex.Message)));
    }

    [Fact]
    public void Closed_ForgeError_RoundTrips_Through_Net11_Closed_JsonPolymorphism()
    {
        ForgeError original = new ForgeError.NotFound("Order", "ord-99", "Order ord-99 not found.");
        string json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<ForgeError>(json);

        var nf = Assert.IsType<ForgeError.NotFound>(roundTripped);
        Assert.Equal("Order", nf.Resource);
        Assert.Equal("ord-99", nf.Id);
    }
}

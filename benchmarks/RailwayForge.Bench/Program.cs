using System.Diagnostics;
using RailwayForge;

// Verifies zero-boxing hot-path performance of RailwayForge's [Union] Result<T, TError>
// vs syntactic boxed C# 15 union types vs exceptions.
const int iterations = 1_000_000;

static Result<int, BenchErr> ComputeZeroBox(int i) =>
    (i & 7) == 0 ? BenchErr.DivisibleByEight : i * 2;

static BoxedUnion<int, BenchErr> ComputeBoxed(int i) =>
    (i & 7) == 0 ? BenchErr.DivisibleByEight : i * 2;

static long RunPipe(int count)
{
    long sum = 0;
    for (int i = 0; i < count; i++)
    {
        sum += ComputeZeroBox(i)
            .Map(static v => v + 1)
            .Ensure(static v => v > 0, BenchErr.NonPositive)
            .Recover(0);
    }
    return sum;
}

// Warmup
long warm = 0;
for (int i = 0; i < 10_000; i++)
{
    warm += ComputeZeroBox(i) switch { int v => v, BenchErr => 0 };
    warm += ComputeBoxed(i) switch { int v => v, BenchErr => 0 };
}
warm += RunPipe(10_000);

// 1. RailwayForge zero-boxing [Union] struct + C# 15 exhaustive switch
long allocBefore = GC.GetAllocatedBytesForCurrentThread();
var sw = Stopwatch.StartNew();
long sum1 = 0;
for (int i = 0; i < iterations; i++)
{
    var r = ComputeZeroBox(i);
    sum1 += r switch { int v => v, BenchErr => 0 };
}
sw.Stop();
long allocZeroBox = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

// 2. Syntactic C# 15 union (object? backing store)
allocBefore = GC.GetAllocatedBytesForCurrentThread();
var swBoxed = Stopwatch.StartNew();
long sum2 = 0;
for (int i = 0; i < iterations; i++)
{
    var r = ComputeBoxed(i);
    sum2 += r switch { int v => v, BenchErr => 0 };
}
swBoxed.Stop();
long allocBoxed = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

// 3. Railway pipeline (Map + Ensure + Recover) with zero-boxing Result<int, BenchErr>
allocBefore = GC.GetAllocatedBytesForCurrentThread();
var swPipe = Stopwatch.StartNew();
long sum3 = RunPipe(iterations);
swPipe.Stop();
long allocPipe = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

Console.WriteLine($"[bench] iterations={iterations:N0} (checksum={sum1 + sum2 + sum3 + warm})");
Console.WriteLine($"[bench] RailwayForge [Union] switch : {sw.Elapsed.TotalMilliseconds,6:F2} ms | {allocZeroBox,12:N0} bytes allocated");
Console.WriteLine($"[bench] RailwayForge pipeline       : {swPipe.Elapsed.TotalMilliseconds,6:F2} ms | {allocPipe,12:N0} bytes allocated");
Console.WriteLine($"[bench] Syntactic boxed C# 15 union : {swBoxed.Elapsed.TotalMilliseconds,6:F2} ms | {allocBoxed,12:N0} bytes allocated");

bool zeroAllocPass = allocZeroBox == 0 && allocPipe == 0;
Console.WriteLine($"[bench] zero-allocation guarantee   : {(zeroAllocPass ? "PASS (0 bytes)" : "FAIL")}");
return zeroAllocPass ? 0 : 1;

enum BenchErr { DivisibleByEight = 1, NonPositive = 2 }

public union BoxedUnion<T, TError>(T, TError);

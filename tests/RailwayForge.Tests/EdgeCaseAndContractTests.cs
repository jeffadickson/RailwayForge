using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RailwayForge.Analyzers;
using RailwayForge.AspNetCore;
using RailwayForge.OpenTelemetry;

namespace RailwayForge.Tests;

public sealed class EdgeCaseAndContractTests
{
    private enum ErrA { One = 1, Two = 2, Three = 3 }
    private sealed record ErrB(string Reason);

    [Fact]
    public void DefaultUninitialized_Result_Throws_Clear_InvalidOperationException()
    {
        Result<int, ErrA> uninit = default;
        Assert.True(uninit.IsUninitialized);
        Assert.False(uninit.HasValue);
        Assert.False(uninit.IsOk);
        Assert.False(uninit.IsError);
        Assert.Equal("Uninitialized", uninit.ToString());
        Assert.Null(uninit.Value);

        Assert.Throws<InvalidOperationException>(() => uninit.Match(v => v, _ => 0));
        Assert.Throws<InvalidOperationException>(() => uninit.Switch(_ => { }, _ => { }));
        Assert.Throws<InvalidOperationException>(() => uninit.Map(x => x + 1));
        Assert.Throws<InvalidOperationException>(() => uninit.MapError(e => new ErrB(e.ToString())));
        Assert.Throws<InvalidOperationException>(() => uninit.Bind(x => Result.Ok<int, ErrA>(x)));
        Assert.Throws<InvalidOperationException>(() => uninit.Ensure(_ => true, ErrA.One));
        Assert.Throws<InvalidOperationException>(() => uninit.Tap(_ => { }));
        Assert.Throws<InvalidOperationException>(() => uninit.TapError(_ => { }));
        Assert.Throws<InvalidOperationException>(() => uninit.Recover(0));
        Assert.Throws<InvalidOperationException>(() => uninit.OrElse(_ => Result.Ok<int, ErrB>(1)));
        Assert.Throws<InvalidOperationException>(() => uninit.DiscardValue());
        Assert.Throws<InvalidOperationException>(() => uninit.TryGetValue(out _, out _));
        Assert.Throws<InvalidOperationException>(() => Result.Combine(uninit, Result.Ok<int, ErrA>(1)));
        Assert.Throws<InvalidOperationException>(() => Result.Collect([uninit]));
        Assert.Throws<InvalidOperationException>(() => Result.Partition([uninit]));

        Result<ErrA> uninitUnit = default;
        Assert.True(uninitUnit.IsUninitialized);
        Assert.False(uninitUnit.HasValue);
        Assert.Equal("Uninitialized", uninitUnit.ToString());
        Assert.Throws<InvalidOperationException>(() => uninitUnit.Match(() => 1, _ => 0));
        Assert.Throws<InvalidOperationException>(() => uninitUnit.Map(() => 1));
        Assert.Throws<InvalidOperationException>(() => uninitUnit.Bind(() => Result.Ok<ErrA>()));
    }

    [Fact]
    public void Equality_HashCode_And_ToString_Are_Consistent()
    {
        Result<int, ErrA> ok1 = 10;
        Result<int, ErrA> ok2 = 10;
        Result<int, ErrA> ok3 = 20;
        Result<int, ErrA> err1 = ErrA.One;
        Result<int, ErrA> err2 = ErrA.One;
        Result<int, ErrA> err3 = ErrA.Two;

        Assert.True(ok1 == ok2);
        Assert.True(ok1 != ok3);
        Assert.True(ok1 != err1);
        Assert.True(err1 == err2);
        Assert.True(err1 != err3);
        Assert.Equal(ok1.GetHashCode(), ok2.GetHashCode());
        Assert.Equal(err1.GetHashCode(), err2.GetHashCode());
        Assert.Equal("Ok(10)", ok1.ToString());
        Assert.Equal("Error(One)", err1.ToString());
        Assert.Equal("()", Unit.Value.ToString());

        Result<ErrA> uOk1 = Unit.Value;
        Result<ErrA> uOk2 = Result.Ok<ErrA>();
        Result<ErrA> uErr = ErrA.Two;
        Assert.True(uOk1 == uOk2);
        Assert.True(uOk1 != uErr);
        Assert.Equal("Ok(())", uOk1.ToString());
        Assert.Equal("Error(Two)", uErr.ToString());
    }

    [Fact]
    public void ShortCircuit_Combinators_Never_Invoke_Wrong_Branch()
    {
        bool okBranchCalled = false;
        bool errBranchCalled = false;
        bool predCalled = false;

        Result<int, ErrA> err = ErrA.Two;
        var stillErr = err
            .Tap(_ => okBranchCalled = true)
            .TapError(_ => errBranchCalled = true)
            .Ensure(_ => { predCalled = true; return true; }, ErrA.One);

        Assert.False(okBranchCalled);
        Assert.True(errBranchCalled);
        Assert.False(predCalled);
        Assert.Equal(err, stillErr);

        var mappedErr = stillErr.MapError(e => new ErrB($"mapped:{e}"));
        Assert.True(mappedErr.TryGetError(out var eb));
        Assert.Equal("mapped:Two", eb.Reason);

        var recovered = mappedErr.OrElse(e => Result.Ok<int, ErrA>(e.Reason.Length));
        Assert.Equal(10, recovered.Match(v => v, _ => -1));

        // Switch & DiscardValue
        int switched = 0;
        recovered.Switch(v => switched = v, _ => switched = -1);
        Assert.Equal(10, switched);
        Assert.True(recovered.DiscardValue().IsOk);
        Assert.True(err.DiscardValue().IsError);
    }

    [Fact]
    public void UnitResult_Combinators_And_TryAction_Work()
    {
        var ok = Result.Ok<ErrA>()
            .Bind(() => Result.Ok<ErrA>())
            .Map(() => 99);
        Assert.Equal(99, ok.Match(v => v, _ => -1));

        bool tapped = false;
        var fail = Result.Fail(ErrA.Three)
            .TapError(_ => tapped = true)
            .MapError(e => new ErrB(e.ToString()))
            .Bind(() => Result.Ok<int, ErrB>(1));
        Assert.True(tapped);
        Assert.True(fail.IsError);

        var tryOk = Result.Try(() => { }, ex => new ErrB(ex.Message));
        Assert.True(tryOk.IsOk);

        var tryFail = Result.Try(() => throw new InvalidOperationException("bad"), ex => new ErrB(ex.Message));
        Assert.True(tryFail.TryGetError(out var e));
        Assert.Equal("bad", e.Reason);
    }

    [Fact]
    public async Task Combine_3_And_4_Tuples_And_Traverse_FailFast_Work()
    {
        var c3Ok = Result.Combine<int, string, bool, ErrA>(1, "x", true);
        Assert.Equal((1, "x", true), c3Ok.Match(v => v, _ => default));

        var c3Err = Result.Combine<int, string, bool, ErrA>(ErrA.One, "x", ErrA.Three);
        Assert.Equal([ErrA.One, ErrA.Three], c3Err.Match(_ => [], errs => errs));

        var c4Ok = Result.Combine<int, int, int, int, ErrA>(1, 2, 3, 4);
        Assert.Equal((1, 2, 3, 4), c4Ok.Match(v => v, _ => default));

        var c4Err = Result.Combine<int, int, int, int, ErrA>(1, ErrA.One, ErrA.Two, ErrA.Three);
        Assert.Equal([ErrA.One, ErrA.Two, ErrA.Three], c4Err.Match(_ => [], errs => errs));

        // TraverseAsync must short-circuit immediately on the first failing element (verified in Dafny).
        int invoked = 0;
        var trav = await Result.TraverseAsync<int, int, ErrA>([1, 2, -1, 4, 5], (x, _) =>
        {
            invoked++;
            return Task.FromResult<Result<int, ErrA>>(x > 0 ? x * 10 : ErrA.Two);
        });
        Assert.Equal(3, invoked); // stopped at index 2 (-1); indices 3 and 4 never ran!
        Assert.True(trav.TryGetError(out var err));
        Assert.Equal(ErrA.Two, err);

        // ValidationBag unit Build() & AddRange
        var bag = new ValidationBag<ErrA>();
        Assert.True(bag.Build().IsOk);
        bag.Check(false, () => ErrA.One).Add(ErrA.Two).AddRange([ErrA.Three]);
        Assert.True(bag.HasErrors);
        Assert.Equal(3, bag.Errors.Count);
        Assert.True(bag.Build().IsError);
    }

    [Fact]
    public async Task Analyzer_Catches_Await_Discards_And_Custom_Union_Result_Types()
    {
        const string source = """
            using System.Runtime.CompilerServices;
            using System.Threading.Tasks;
            using RailwayForge;

            [Union]
            public readonly struct CustomResult : IUnion
            {
                private readonly byte _s;
                public CustomResult(Unit _) => _s = 1;
                public CustomResult(string e) => _s = 2;
                public bool HasValue => _s != 0;
                public object? Value => _s == 1 ? Unit.Value : null;
            }

            public static class AsyncDemo
            {
                public static Task<Result<int, ForgeError>> DoAsync() =>
                    Task.FromResult<Result<int, ForgeError>>(1);
                public static CustomResult CustomOp() => new CustomResult(Unit.Value);

                public static async Task BadAsync()
                {
                    await DoAsync();            // RF0001 on awaited Result!
                    CustomOp();                 // RF0001 on custom [Union] *Result!
                    CustomResult c = default;   // RF0002 on custom [Union] *Result!
                    _ = c;
                }
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Result<,>).Assembly.Location),
        };
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string sysRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(sysRuntime))
            refs.Add(MetadataReference.CreateFromFile(sysRuntime));

        var comp = CSharpCompilation.Create("EdgeAnalyzerTest", [tree], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diags = await comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new ResultUsageAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Equal(2, diags.Count(d => d.Id == ResultUsageAnalyzer.UnusedResultId));
        Assert.Single(diags, d => d.Id == ResultUsageAnalyzer.DefaultResultId);
    }

    [Theory]
    [InlineData("validation", HttpStatusCode.BadRequest)]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    [InlineData("notfound", HttpStatusCode.NotFound)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable)]
    [InlineData("unexpected", HttpStatusCode.InternalServerError)]
    [InlineData("nocontent", HttpStatusCode.NoContent)]
    public async Task AspNetCore_Maps_Every_ForgeError_Variant_And_NoContent(string route, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        await using var app = builder.Build();
        app.MapGet("/validation", () => Result.Fail<int, ForgeError>(new ForgeError.Validation("f", "m")).ToHttpResult());
        app.MapGet("/unauthorized", () => Result.Fail<int, ForgeError>(new ForgeError.Unauthorized("m")).ToHttpResult());
        app.MapGet("/forbidden", () => Result.Fail<int, ForgeError>(new ForgeError.Forbidden("op", "m")).ToHttpResult());
        app.MapGet("/notfound", () => Result.Fail<int, ForgeError>(new ForgeError.NotFound("r", "1", "m")).ToHttpResult());
        app.MapGet("/conflict", () => Result.Fail<int, ForgeError>(new ForgeError.Conflict("t", "m")).ToHttpResult());
        app.MapGet("/unavailable", () => Result.Fail<int, ForgeError>(new ForgeError.Unavailable("d", "m")).ToHttpResult());
        app.MapGet("/unexpected", () => Result.Fail<int, ForgeError>(new ForgeError.Unexpected("m", "IOException")).ToHttpResult());
        app.MapGet("/nocontent", () => Result.Ok<ForgeError>().ToHttpResult());

        await app.StartAsync();
        using var client = app.GetTestClient();

        var resp = await client.GetAsync("/" + route);
        Assert.Equal(expected, resp.StatusCode);
    }

    [Fact]
    public void OpenTelemetry_Records_Custom_NonForgeError_And_DurationHistogram()
    {
        using var source = new ActivitySource("RailwayForge.EdgeTests");
        Activity? last = null;
        using var al = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => last = a,
        };
        ActivitySource.AddActivityListener(al);

        int durationSamples = 0;
        using var ml = new MeterListener();
        ml.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == RailwayTelemetry.MeterName) l.EnableMeasurementEvents(inst);
        };
        ml.SetMeasurementEventCallback<double>((inst, _, _, _) =>
        {
            if (inst.Name == "railway.results.duration_ms") Interlocked.Increment(ref durationSamples);
        });
        ml.Start();

        var r = source.Trace<int, ErrA>("custom.err", () => ErrA.Two);
        Assert.True(r.IsError);
        Assert.NotNull(last);
        Assert.Equal(ActivityStatusCode.Error, last!.Status);
        Assert.Equal("ErrA", last.GetTagItem("error.code"));
        Assert.True(durationSamples >= 1);
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RailwayForge.Analyzers;

namespace RailwayForge.Tests;

public sealed class AnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Result<,>).Assembly.Location),
        };
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string sysRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(sysRuntime))
            refs.Add(MetadataReference.CreateFromFile(sysRuntime));

        var compilation = CSharpCompilation.Create(
            "AnalyzerTestAssembly",
            [syntaxTree],
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new ResultUsageAnalyzer()));

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    [Fact]
    public async Task RF0001_Flags_Discarded_Result_And_Allows_Explicit_Discard()
    {
        const string code = """
            using RailwayForge;
            public sealed record Err(string Msg);
            public static class Demo
            {
                public static Result<int, Err> Work() => 42;
                public static void Bad()
                {
                    Work(); // RF0001
                }
                public static void Good()
                {
                    _ = Work();
                    var r = Work();
                    _ = r.IsOk;
                }
            }
            """;

        var diags = await RunAnalyzerAsync(code);
        var rf0001 = Assert.Single(diags, d => d.Id == ResultUsageAnalyzer.UnusedResultId);
        Assert.Contains("Result<int, Err>", rf0001.GetMessage());
    }

    [Fact]
    public async Task RF0002_Flags_Default_And_Parameterless_New_Result()
    {
        const string code = """
            using RailwayForge;
            public sealed record Err(string Msg);
            public static class Demo
            {
                public static Result<int, Err> Bad1() => default;
                public static Result<int, Err> Bad2() => new Result<int, Err>();
                public static Result<int, Err> Good() => 7;
            }
            """;

        var diags = await RunAnalyzerAsync(code);
        var rf0002 = diags.Where(d => d.Id == ResultUsageAnalyzer.DefaultResultId).ToList();
        Assert.Equal(2, rf0002.Count);
    }

    [Fact]
    public async Task RF0003_Flags_SameType_Result_Ambiguity()
    {
        const string code = """
            using RailwayForge;
            public static class Demo
            {
                public static Result<string, string> Bad() => Result.Ok<string, string>("ok");
                public static Result<string, ForgeError> Good() => "ok";
            }
            """;

        var diags = await RunAnalyzerAsync(code);
        Assert.Single(diags, d => d.Id == ResultUsageAnalyzer.SameTypeResultId);
    }
}

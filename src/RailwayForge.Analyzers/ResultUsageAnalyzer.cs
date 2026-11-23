using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RailwayForge.Analyzers;

/// <summary>
/// Compile-time Errors-as-Values enforcement for RailwayForge and C# 15 [Union] Result types:
///   RF0001: Result return values must be inspected, returned, or explicitly discarded with '_ = ...'.
///   RF0002: Result union structs must not be constructed via default or parameterless new().
///   RF0003: Result&lt;T, TError&gt; should not use identical types for T and TError (ambiguous union matching).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultUsageAnalyzer : DiagnosticAnalyzer
{
    public const string UnusedResultId = "RF0001";
    public const string DefaultResultId = "RF0002";
    public const string SameTypeResultId = "RF0003";

    private static readonly DiagnosticDescriptor UnusedResultRule = new(
        id: UnusedResultId,
        title: "Result value is discarded without inspection",
        messageFormat: "Return value of type '{0}' must be inspected, returned, or explicitly discarded with '_ = ...'",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DefaultResultRule = new(
        id: DefaultResultId,
        title: "Uninitialized default Result union",
        messageFormat: "Do not construct '{0}' with default or parameterless new(); return an explicit value or error instead",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SameTypeResultRule = new(
        id: SameTypeResultId,
        title: "Result<T, TError> uses identical success and error types",
        messageFormat: "Result type '{0}' uses the same type '{1}' for both value and error; use a distinct error type so union conversions and switch matching remain unambiguous",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedResultRule, DefaultResultRule, SameTypeResultRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeExpressionStatement, SyntaxKind.ExpressionStatement);
        context.RegisterSyntaxNodeAction(AnalyzeDefaultExpression,
            SyntaxKind.DefaultExpression,
            SyntaxKind.DefaultLiteralExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
    }

    private static void AnalyzeExpressionStatement(SyntaxNodeAnalysisContext context)
    {
        var stmt = (ExpressionStatementSyntax)context.Node;
        var expr = stmt.Expression;

        // Explicit discards (_ = Foo()) and assignments are intentional.
        if (expr is AssignmentExpressionSyntax) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(expr, context.CancellationToken);
        if (IsResultType(typeInfo.Type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnusedResultRule,
                expr.GetLocation(),
                typeInfo.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static void AnalyzeDefaultExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is ObjectCreationExpressionSyntax oce && oce.ArgumentList?.Arguments.Count > 0)
            return;
        if (context.Node is ImplicitObjectCreationExpressionSyntax ioce && ioce.ArgumentList.Arguments.Count > 0)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken);
        var type = typeInfo.ConvertedType ?? typeInfo.Type;
        if (IsResultType(type))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DefaultResultRule,
                context.Node.GetLocation(),
                type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var generic = (GenericNameSyntax)context.Node;
        if (generic.TypeArgumentList.Arguments.Count != 2) return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(generic, context.CancellationToken);
        if (symbolInfo.Symbol is not INamedTypeSymbol named) return;
        if (!IsRailwayResultTwoArity(named)) return;

        var tVal = named.TypeArguments[0];
        var tErr = named.TypeArguments[1];
        if (tVal.TypeKind != TypeKind.TypeParameter &&
            tErr.TypeKind != TypeKind.TypeParameter &&
            SymbolEqualityComparer.Default.Equals(tVal, tErr))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SameTypeResultRule,
                generic.GetLocation(),
                named.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                tVal.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static bool IsRailwayResultTwoArity(INamedTypeSymbol named) =>
        named.Arity == 2 &&
        named.Name == "Result" &&
        named.ContainingNamespace?.ToDisplayString() == "RailwayForge";

    private static bool IsResultType(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named) return false;
        var ns = named.ContainingNamespace?.ToDisplayString();
        if (ns == "RailwayForge" && named.Name == "Result")
            return true;

        // Also enforce on custom C# 15 [Union] structs following the *Result naming convention.
        if (named.IsValueType && named.Name.EndsWith("Result", System.StringComparison.Ordinal))
        {
            bool hasUnionAttr = named.GetAttributes().Any(a =>
                a.AttributeClass?.Name == "UnionAttribute" &&
                a.AttributeClass.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices");
            if (hasUnionAttr) return true;
        }

        return false;
    }
}

using System.Collections.Immutable;
using System.Linq;
using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Breakwater.Analyzers;

/// <summary>Reports unsafe operations in EF Core migrations.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MigrationAnalyzer : DiagnosticAnalyzer
{
    private const string MigrationBuilderMetadataName = "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        RuleCatalog.All.Select(rule => rule.Descriptor).ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Projects without EF Core have nothing to analyze.
        var migrationBuilder = context.Compilation.GetTypeByMetadataName(MigrationBuilderMetadataName);
        if (migrationBuilder is null)
        {
            return;
        }

        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(operationContext, migrationBuilder),
            OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol migrationBuilder)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (IsInsideDownMethod(invocation))
        {
            return;
        }

        var operation = MigrationOperationReader.Read(invocation, migrationBuilder);
        if (operation is null)
        {
            return;
        }

        foreach (var rule in RuleCatalog.All)
        {
            var messageArguments = rule.Check(operation);
            if (messageArguments is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, operation.Location, messageArguments));
            }
        }
    }

    /// <summary>Undoing a migration is rarely run in production, so <c>Down</c> is not analyzed.</summary>
    private static bool IsInsideDownMethod(IInvocationOperation invocation)
    {
        var method = invocation.Syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return method?.Identifier.ValueText == "Down";
    }
}

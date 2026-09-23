using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// One <see cref="MigrationContext"/> per <c>Up</c> method, built the first time any of its
    /// operations is analyzed and reused by the rest: rules that need to know which tables were
    /// created earlier in the same migration do not each rescan the method.
    /// </summary>
    private static readonly ConditionalWeakTable<SyntaxNode, MigrationContext> ContextCache = new();

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
        var enclosingMethod = invocation.Syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (enclosingMethod?.Identifier.ValueText == "Down")
        {
            return;
        }

        var operation = MigrationOperationReader.Read(invocation, migrationBuilder);
        if (operation is null)
        {
            return;
        }

        var migrationContext = enclosingMethod is null
            ? new MigrationContext(new HashSet<string>())
            : ContextCache.GetValue(enclosingMethod, node => BuildContext(node, invocation.SemanticModel!, migrationBuilder));

        var alterColumnReported = false;
        foreach (var rule in RuleCatalog.All)
        {
            var messageArguments = rule.Check(operation, migrationContext);
            if (messageArguments is null)
            {
                continue;
            }

            if (RuleCatalog.AlterColumnExclusiveGroup.Contains(rule.Descriptor.Id))
            {
                if (alterColumnReported)
                {
                    continue;
                }

                alterColumnReported = true;
            }

            context.ReportDiagnostic(Diagnostic.Create(rule.Descriptor, operation.Location, messageArguments));
        }
    }

    /// <summary>
    /// Scans one <c>Up</c> method (or local function/lambda inside it - those are not separate
    /// migration scopes) for <c>CreateTable</c> calls, so later rules know which tables are still
    /// empty. Runs once per method: the result is cached by <see cref="ContextCache"/>.
    /// </summary>
    private static MigrationContext BuildContext(SyntaxNode methodNode, SemanticModel semanticModel, INamedTypeSymbol migrationBuilder)
    {
        var tablesCreated = new HashSet<string>();
        foreach (var invocationSyntax in methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetOperation(invocationSyntax) is not IInvocationOperation invocationOperation)
            {
                continue;
            }

            var operation = MigrationOperationReader.Read(invocationOperation, migrationBuilder);
            if (operation is { Kind: MigrationOperationKind.CreateTable })
            {
                tablesCreated.Add(operation.QualifiedTable);
            }
        }

        var suppressTransaction = HasSuppressTransactionAssignment(methodNode, semanticModel, migrationBuilder);
        return new MigrationContext(tablesCreated, suppressTransaction);
    }

    /// <summary>
    /// True when the method sets <c>migrationBuilder.SuppressTransaction = true;</c> anywhere,
    /// the flag a Postgres <c>CREATE INDEX CONCURRENTLY</c> needs alongside the
    /// <c>Npgsql:CreatedConcurrently</c> annotation.
    /// </summary>
    private static bool HasSuppressTransactionAssignment(SyntaxNode methodNode, SemanticModel semanticModel, INamedTypeSymbol migrationBuilder)
    {
        foreach (var assignment in methodNode.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "SuppressTransaction" } memberAccess)
            {
                continue;
            }

            if (semanticModel.GetOperation(assignment.Right)?.ConstantValue is not { HasValue: true, Value: true })
            {
                continue;
            }

            var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
            if (SymbolEqualityComparer.Default.Equals(receiverType, migrationBuilder))
            {
                return true;
            }
        }

        return false;
    }
}

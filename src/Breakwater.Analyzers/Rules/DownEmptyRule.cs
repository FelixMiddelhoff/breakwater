using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW011: an empty or <c>NotSupportedException</c>-throwing <c>Down</c> means the migration
/// cannot be rolled back. Informational only, off by default (many teams never roll back).
/// This is the one other exception (besides BW028/BW032) to "never analyze what is not a
/// migration Up", so - like them - it does not fit the per-operation <see cref="IMigrationRule"/>
/// shape and is wired up directly against the <c>Down</c> method's syntax.
/// </summary>
internal static class DownEmptyRule
{
    public static readonly DiagnosticDescriptor Descriptor = RuleDescriptors.Create(
        "BW011",
        "Down is empty or always throws",
        "Migration '{0}' cannot be rolled back: Down is empty or throws NotSupportedException",
        "Without a working Down, a bad deploy cannot be rolled back with 'dotnet ef database update' " +
        "to the previous migration; someone has to write and test the reverse by hand under pressure. " +
        "Informational: many teams intentionally never roll back and can ignore this.",
        isEnabledByDefault: false);

    public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol migrationType)
    {
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, migrationType),
            SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol migrationType)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.ValueText != "Down")
        {
            return;
        }

        var containingType = method.Parent as ClassDeclarationSyntax;
        var typeSymbol = containingType is null ? null : context.SemanticModel.GetDeclaredSymbol(containingType);
        if (typeSymbol is null || !InheritsFromMigration(typeSymbol, migrationType))
        {
            return;
        }

        if (!IsEmptyOrAlwaysThrows(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, containingType!.Identifier.GetLocation(), typeSymbol.Name));
    }

    private static bool InheritsFromMigration(INamedTypeSymbol type, INamedTypeSymbol migrationType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, migrationType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmptyOrAlwaysThrows(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is { } expressionBody)
        {
            return IsNotSupportedThrow(expressionBody.Expression);
        }

        if (method.Body is not { } body)
        {
            return false;
        }

        if (body.Statements.Count == 0)
        {
            return true;
        }

        return body.Statements.Count == 1 && body.Statements[0] is ThrowStatementSyntax { Expression: { } thrown } && IsNotSupportedException(thrown);
    }

    private static bool IsNotSupportedThrow(ExpressionSyntax expression) =>
        expression is ThrowExpressionSyntax { Expression: { } thrown } && IsNotSupportedException(thrown);

    private static bool IsNotSupportedException(ExpressionSyntax expression) =>
        expression is ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax { Identifier.ValueText: "NotSupportedException" } }
        || expression is ObjectCreationExpressionSyntax { Type: QualifiedNameSyntax { Right.Identifier.ValueText: "NotSupportedException" } };
}

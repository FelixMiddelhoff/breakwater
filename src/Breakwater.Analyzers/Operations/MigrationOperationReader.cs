using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Breakwater.Analyzers.Operations;

/// <summary>Turns a call on <c>MigrationBuilder</c> into a <see cref="MigrationOperation"/>.</summary>
internal static class MigrationOperationReader
{
    /// <summary>
    /// Returns null when the call is not a migration operation Breakwater knows.
    /// Arguments are found by parameter name, so named and reordered arguments work.
    /// </summary>
    public static MigrationOperation? Read(IInvocationOperation invocation, INamedTypeSymbol migrationBuilderType)
    {
        var method = invocation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, migrationBuilderType))
        {
            return null;
        }

        var location = invocation.Syntax.GetLocation();
        switch (method.Name)
        {
            case "DropColumn":
                return new MigrationOperation(
                    MigrationOperationKind.DropColumn,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "DropTable":
                return new MigrationOperation(
                    MigrationOperationKind.DropTable,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "name"),
                    column: null,
                    newName: null,
                    location);

            case "RenameColumn":
                return new MigrationOperation(
                    MigrationOperationKind.RenameColumn,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    ReadName(invocation, "newName"),
                    location);

            case "RenameTable":
                return new MigrationOperation(
                    MigrationOperationKind.RenameTable,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "name"),
                    column: null,
                    ReadOptionalText(invocation, "newName"),
                    location);

            default:
                return null;
        }
    }

    /// <summary>A required name; <see cref="MigrationOperation.UnknownName"/> if it is not a constant.</summary>
    private static string ReadName(IInvocationOperation invocation, string parameterName)
    {
        return ReadOptionalText(invocation, parameterName) ?? MigrationOperation.UnknownName;
    }

    /// <summary>The constant string passed for a parameter, or null if omitted, null or not constant.</summary>
    private static string? ReadOptionalText(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        var constant = argument?.Value.ConstantValue;
        return constant is { HasValue: true, Value: string text } ? text : null;
    }
}

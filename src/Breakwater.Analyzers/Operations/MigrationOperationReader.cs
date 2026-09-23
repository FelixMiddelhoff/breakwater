using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

            case "CreateTable":
                return new MigrationOperation(
                    MigrationOperationKind.CreateTable,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "name"),
                    column: null,
                    newName: null,
                    location);

            case "AddColumn":
                return new MigrationOperation(
                    MigrationOperationKind.AddColumn,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location)
                {
                    ClrType = ReadTypeArgument(invocation),
                    Nullable = ReadBool(invocation, "nullable"),
                    MaxLength = ReadInt(invocation, "maxLength"),
                    Precision = ReadInt(invocation, "precision"),
                    Scale = ReadInt(invocation, "scale"),
                    Collation = ReadOptionalText(invocation, "collation"),
                    HasDefaultValue = HasArgument(invocation, "defaultValue"),
                    HasDefaultValueSql = HasArgument(invocation, "defaultValueSql"),
                    DefaultValueSql = ReadOptionalText(invocation, "defaultValueSql"),
                    IsNpgsql = IsGuardedByIsNpgsql(invocation),
                };

            case "AlterColumn":
                return new MigrationOperation(
                    MigrationOperationKind.AlterColumn,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location)
                {
                    ClrType = ReadTypeArgument(invocation),
                    OldClrType = ReadOldTypeArgument(invocation),
                    Nullable = ReadBool(invocation, "nullable"),
                    OldNullable = ReadBool(invocation, "oldNullable"),
                    MaxLength = ReadInt(invocation, "maxLength"),
                    OldMaxLength = ReadInt(invocation, "oldMaxLength"),
                    Precision = ReadInt(invocation, "precision"),
                    OldPrecision = ReadInt(invocation, "oldPrecision"),
                    Scale = ReadInt(invocation, "scale"),
                    OldScale = ReadInt(invocation, "oldScale"),
                    Collation = ReadOptionalText(invocation, "collation"),
                    OldCollation = ReadOptionalText(invocation, "oldCollation"),
                    HasDefaultValue = HasArgument(invocation, "defaultValue"),
                    HasDefaultValueSql = HasArgument(invocation, "defaultValueSql"),
                    DefaultValueSql = ReadOptionalText(invocation, "defaultValueSql"),
                    IsNpgsql = IsGuardedByIsNpgsql(invocation),
                };

            case "CreateIndex":
                return new MigrationOperation(
                    MigrationOperationKind.CreateIndex,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location)
                {
                    Unique = ReadBool(invocation, "unique"),
                    IsCreatedConcurrently = HasChainedAnnotation(invocation, "Npgsql:CreatedConcurrently"),
                    IsOnline = HasChainedAnnotation(invocation, "SqlServer:Online"),
                };

            case "AddForeignKey":
                return new MigrationOperation(
                    MigrationOperationKind.AddForeignKey,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "AddCheckConstraint":
                return new MigrationOperation(
                    MigrationOperationKind.AddCheckConstraint,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "AddUniqueConstraint":
                return new MigrationOperation(
                    MigrationOperationKind.AddUniqueConstraint,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "AddPrimaryKey":
                return new MigrationOperation(
                    MigrationOperationKind.AddPrimaryKey,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "DropPrimaryKey":
                return new MigrationOperation(
                    MigrationOperationKind.DropPrimaryKey,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "DropUniqueConstraint":
                return new MigrationOperation(
                    MigrationOperationKind.DropUniqueConstraint,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "DropCheckConstraint":
                return new MigrationOperation(
                    MigrationOperationKind.DropCheckConstraint,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            case "DropForeignKey":
                return new MigrationOperation(
                    MigrationOperationKind.DropForeignKey,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location);

            default:
                return null;
        }
    }

    /// <summary>
    /// True when this call is immediately chained with <c>.Annotation(annotationName, true)</c>,
    /// the pattern EF uses for provider-specific opt-ins on the operation builder returned by
    /// calls such as <c>CreateIndex</c> (e.g. <c>migrationBuilder.CreateIndex(...).Annotation(...)</c>).
    /// </summary>
    private static bool HasChainedAnnotation(IInvocationOperation invocation, string annotationName)
    {
        if (invocation.Parent is not IInvocationOperation outer || outer.TargetMethod.Name != "Annotation")
        {
            return false;
        }

        var name = ReadConstant(outer, "name");
        var value = ReadConstant(outer, "value");
        return name is string nameText && nameText == annotationName && value is bool flag && flag;
    }

    private static object? ReadConstant(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument is null)
        {
            return null;
        }

        var value = argument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        var constant = value.ConstantValue;
        return constant.HasValue ? constant.Value : null;
    }

    /// <summary>The <c>T</c> in <c>AddColumn&lt;T&gt;</c> / <c>AlterColumn&lt;T&gt;</c>, display-formatted.</summary>
    private static string? ReadTypeArgument(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        return method.TypeArguments.Length == 1 ? method.TypeArguments[0].ToDisplayString() : null;
    }

    /// <summary>The type behind AlterColumn's <c>oldClrType</c> parameter (a <c>typeof(...)</c> expression).</summary>
    private static string? ReadOldTypeArgument(IInvocationOperation invocation)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "oldClrType");
        return argument?.Value is ITypeOfOperation typeOf ? typeOf.TypeOperand.ToDisplayString() : null;
    }

    private static bool ReadBool(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        var constant = argument?.Value.ConstantValue;
        return constant is { HasValue: true, Value: bool value } && value;
    }

    private static int? ReadInt(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument is null)
        {
            return null;
        }

        // int literals passed for an `int?` parameter go through an implicit nullable
        // conversion; unwrap it so the constant folds the same way it does for a non-nullable
        // parameter.
        var value = argument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        var constant = value.ConstantValue;
        return constant is { HasValue: true, Value: int intValue } ? intValue : null;
    }

    /// <summary>
    /// True when an explicit, non-default argument was supplied for <paramref name="parameterName"/>,
    /// regardless of whether its value is a compile-time constant: a computed default still counts
    /// as "there is a default", which is what BW005/BW006 need to know.
    /// </summary>
    private static bool HasArgument(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument is null || argument.IsImplicit)
        {
            return false;
        }

        // A literal null argument means "no default", even though it was written explicitly.
        var constant = argument.Value.ConstantValue;
        return !(constant is { HasValue: true, Value: null });
    }

    /// <summary>
    /// Heuristic provider signal: the call sits inside an <c>if (migrationBuilder.IsNpgsql())</c>
    /// branch, the common pattern for migrations that support more than one provider. Nothing else
    /// is treated as "known Postgres" today, so provider-specific rules stay silent otherwise.
    /// </summary>
    private static bool IsGuardedByIsNpgsql(IInvocationOperation invocation)
    {
        foreach (var ifStatement in invocation.Syntax.Ancestors().OfType<IfStatementSyntax>())
        {
            if (ifStatement.Condition.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(IsIsNpgsqlCall))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIsNpgsqlCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "IsNpgsql",
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "IsNpgsql",
            _ => false,
        };
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

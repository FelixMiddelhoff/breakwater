using System;
using System.Collections.Immutable;
using System.Linq;
using Breakwater.Analyzers.Configuration;
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
    /// <param name="providerOverride">
    /// The configured <c>breakwater_provider</c> value. When not <c>Auto</c>, it is trusted
    /// outright for this operation's <see cref="MigrationOperation.IsNpgsql"/>/<see cref="MigrationOperation.IsMySql"/>/
    /// <see cref="MigrationOperation.DetectedProvider"/> - the guard-detection heuristic below is
    /// skipped entirely, so provider-aware rules fire even without an <c>if (migrationBuilder.IsXxx())</c>
    /// guard around the call.
    /// </param>
    public static MigrationOperation? Read(
        IInvocationOperation invocation,
        INamedTypeSymbol migrationBuilderType,
        BreakwaterDatabaseProvider providerOverride = BreakwaterDatabaseProvider.Auto)
    {
        var method = invocation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, migrationBuilderType))
        {
            return null;
        }

        var location = invocation.Syntax.GetLocation();
        var operation = ReadCore(invocation, method, location);
        if (operation is not null)
        {
            ApplyProvider(operation, invocation, providerOverride);
        }

        return operation;
    }

    private static MigrationOperation? ReadCore(IInvocationOperation invocation, IMethodSymbol method, Location location)
    {
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
                    IsMySql = IsGuardedByIsMySql(invocation),
                    HasPlaceholderDefaultValue = IsPlaceholderDefault(invocation, "defaultValue"),
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
                    IsMySql = IsGuardedByIsMySql(invocation),
                    AnnotationNames = ReadChainedAnnotationNames(invocation, old: false),
                    OldAnnotationNames = ReadChainedAnnotationNames(invocation, old: true),
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
                    IsNpgsql = IsGuardedByIsNpgsql(invocation),
                    IsMySql = IsGuardedByIsMySql(invocation),
                    ForeignKeyColumn = ReadFirstArrayElement(invocation, "columns"),
                };

            case "AddForeignKey":
                return new MigrationOperation(
                    MigrationOperationKind.AddForeignKey,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    ReadName(invocation, "name"),
                    newName: null,
                    location)
                {
                    ForeignKeyColumn = ReadFirstArrayElement(invocation, "columns"),
                    OnDeleteCascade = IsCascade(invocation),
                };

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

            case "Sql":
                return new MigrationOperation(
                    MigrationOperationKind.Sql,
                    schema: null,
                    table: string.Empty,
                    column: null,
                    newName: null,
                    location)
                {
                    // ReadOptionalText already relies on Roslyn's own constant folding, which
                    // covers plain/verbatim/raw string literals, constant interpolated strings
                    // (C# 10+) and concatenation of constant operands across lines. Anything
                    // else (a variable, File.ReadAllText, a resource) is not a compile-time
                    // constant and comes back null, so BW010 stays silent for it.
                    SqlText = ReadOptionalText(invocation, "sql"),
                    SqlSuppressesTransaction = ReadBool(invocation, "suppressTransaction"),
                    IsNpgsql = IsGuardedByIsNpgsql(invocation),
                    IsMySql = IsGuardedByIsMySql(invocation),
                };

            case "InsertData":
                return new MigrationOperation(
                    MigrationOperationKind.InsertData,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    column: null,
                    newName: null,
                    location)
                {
                    RowCount = ReadRowCount(invocation, "values"),
                };

            case "UpdateData":
                return new MigrationOperation(
                    MigrationOperationKind.UpdateData,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    column: null,
                    newName: null,
                    location)
                {
                    RowCount = ReadRowCount(invocation, "keyValues") ?? ReadRowCount(invocation, "values"),
                };

            case "DeleteData":
                return new MigrationOperation(
                    MigrationOperationKind.DeleteData,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "table"),
                    column: null,
                    newName: null,
                    location)
                {
                    RowCount = ReadRowCount(invocation, "keyValues"),
                };

            case "DropSchema":
                return new MigrationOperation(
                    MigrationOperationKind.DropSchema,
                    schema: null,
                    ReadName(invocation, "name"),
                    column: null,
                    newName: null,
                    location);

            case "DropSequence":
                return new MigrationOperation(
                    MigrationOperationKind.DropSequence,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "name"),
                    column: null,
                    newName: null,
                    location);

            case "AlterSequence":
            case "RestartSequence":
                return new MigrationOperation(
                    MigrationOperationKind.AlterSequence,
                    ReadOptionalText(invocation, "schema"),
                    ReadName(invocation, "name"),
                    column: null,
                    newName: null,
                    location);

            case "AlterDatabase":
                return new MigrationOperation(
                    MigrationOperationKind.AlterDatabase,
                    schema: null,
                    table: string.Empty,
                    column: null,
                    newName: null,
                    location);

            default:
                return null;
        }
    }

    /// <summary>
    /// Resolves <see cref="MigrationOperation.DetectedProvider"/> (and the legacy
    /// <see cref="MigrationOperation.IsNpgsql"/>/<see cref="MigrationOperation.IsMySql"/> booleans
    /// every existing provider-aware rule already reads) for one operation.
    /// </summary>
    private static void ApplyProvider(MigrationOperation operation, IInvocationOperation invocation, BreakwaterDatabaseProvider providerOverride)
    {
        if (providerOverride != BreakwaterDatabaseProvider.Auto)
        {
            // An explicit override is trusted outright: skip the guard-detection heuristic
            // entirely, so rules fire even when the migration does not wrap the call in an
            // `if (migrationBuilder.IsXxx())` guard.
            operation.DetectedProvider = providerOverride;
            operation.IsNpgsql = providerOverride == BreakwaterDatabaseProvider.Postgres;
            operation.IsMySql = providerOverride == BreakwaterDatabaseProvider.MySql;
            return;
        }

        // Auto: the guard heuristic already set IsNpgsql/IsMySql for the operation kinds that
        // support it (see the switch above).
        if (operation.IsNpgsql)
        {
            operation.DetectedProvider = BreakwaterDatabaseProvider.Postgres;
            return;
        }

        if (operation.IsMySql)
        {
            operation.DetectedProvider = BreakwaterDatabaseProvider.MySql;
            return;
        }

        // Fall back to provider-specific chained annotations (already captured for AlterColumn/
        // CreateIndex; read directly here for kinds that do not capture AnnotationNames).
        var annotationNames = operation.AnnotationNames.Count > 0 || operation.OldAnnotationNames.Count > 0
            ? operation.AnnotationNames.Concat(operation.OldAnnotationNames)
            : ReadChainedAnnotationNames(invocation, old: false).Concat(ReadChainedAnnotationNames(invocation, old: true));

        foreach (var name in annotationNames)
        {
            if (name.StartsWith("Npgsql:", StringComparison.Ordinal))
            {
                operation.DetectedProvider = BreakwaterDatabaseProvider.Postgres;
                operation.IsNpgsql = true;
                return;
            }

            if (name.StartsWith("SqlServer:", StringComparison.Ordinal))
            {
                operation.DetectedProvider = BreakwaterDatabaseProvider.SqlServer;
                return;
            }

            if (name.StartsWith("MySql:", StringComparison.OrdinalIgnoreCase))
            {
                operation.DetectedProvider = BreakwaterDatabaseProvider.MySql;
                operation.IsMySql = true;
                return;
            }

            if (name.StartsWith("Sqlite:", StringComparison.OrdinalIgnoreCase))
            {
                operation.DetectedProvider = BreakwaterDatabaseProvider.Sqlite;
                return;
            }
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

    /// <summary>
    /// True when the call sits inside an <c>if (migrationBuilder.IsMySql())</c> branch, the same
    /// pattern <see cref="IsGuardedByIsNpgsql"/> uses for PostgreSQL.
    /// </summary>
    private static bool IsGuardedByIsMySql(IInvocationOperation invocation)
    {
        foreach (var ifStatement in invocation.Syntax.Ancestors().OfType<IfStatementSyntax>())
        {
            if (ifStatement.Condition.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(candidate => candidate.Expression switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "IsMySql",
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "IsMySql",
                    _ => false,
                }))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The first element of a constant <c>string[] { ... }</c> array argument, or null.</summary>
    private static string? ReadFirstArrayElement(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument?.Value is not IArrayCreationOperation { Initializer.ElementValues: { Length: > 0 } elements })
        {
            return null;
        }

        var constant = elements[0].ConstantValue;
        return constant is { HasValue: true, Value: string text } ? text : null;
    }

    /// <summary>
    /// True when <c>onDelete</c> was passed <c>ReferentialAction.Cascade</c> (constant value 1)
    /// or the equivalent literal int.
    /// </summary>
    private static bool IsCascade(IInvocationOperation invocation)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "onDelete");
        if (argument is null)
        {
            return false;
        }

        var value = argument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        // ReferentialAction.Cascade is field ordinal 1; a direct field reference also matches by name.
        if (value is IFieldReferenceOperation { Field.Name: "Cascade" })
        {
            return true;
        }

        var constant = value.ConstantValue;
        return constant is { HasValue: true, Value: int intValue } && intValue == 1;
    }

    /// <summary>
    /// True when <paramref name="parameterName"/> is a recognizable constant placeholder: a
    /// numeric zero, an empty string, or <c>Guid.Empty</c>.
    /// </summary>
    private static bool IsPlaceholderDefault(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument is null || argument.IsImplicit)
        {
            return false;
        }

        var value = argument.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        if (value is IFieldReferenceOperation { Field.Name: "Empty", Field.ContainingType.Name: "Guid" })
        {
            return true;
        }

        var constant = value.ConstantValue;
        return constant.HasValue && constant.Value switch
        {
            string s => s.Length == 0,
            int i => i == 0,
            long l => l == 0,
            short sh => sh == 0,
            byte b => b == 0,
            _ => false,
        };
    }

    /// <summary>
    /// Collects the names from every <c>.Annotation(name, value)</c> (or <c>.OldAnnotation</c>
    /// when <paramref name="old"/> is true) chained onto this invocation, following the fluent
    /// chain outward through as many links as are present.
    /// </summary>
    private static ImmutableHashSet<string> ReadChainedAnnotationNames(IInvocationOperation invocation, bool old)
    {
        var names = ImmutableHashSet.CreateBuilder<string>();
        var methodName = old ? "OldAnnotation" : "Annotation";
        IOperation? current = invocation.Parent;
        while (current is IInvocationOperation outer)
        {
            if (outer.TargetMethod.Name == methodName && ReadConstant(outer, "name") is string name)
            {
                names.Add(name);
            }

            // Keep following the chain even past unrelated calls (e.g. the other annotation kind)
            // so `.Annotation(...).OldAnnotation(...)` and any order of both are both captured.
            current = outer.Parent is IInvocationOperation ? outer.Parent : null;
        }

        return names.ToImmutable();
    }

    /// <summary>
    /// Counts the rows in a constant row-data array argument (<c>InsertData</c>'s <c>values</c>,
    /// <c>UpdateData</c>'s <c>keyValues</c>/<c>values</c>, <c>DeleteData</c>'s <c>keyValues</c>):
    /// a 2-D array creation's outer length when it is a literal size, or a jagged/1-D array
    /// creation's element count. Null when the row count is not statically knowable.
    /// </summary>
    private static int? ReadRowCount(IInvocationOperation invocation, string parameterName)
    {
        var argument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == parameterName);
        if (argument?.Value is not IArrayCreationOperation arrayCreation)
        {
            return null;
        }

        // A 2-D `object[,]` (EF's default multi-row shape): the first dimension size, if constant.
        if (arrayCreation.DimensionSizes.Length == 2)
        {
            var sizeConstant = arrayCreation.DimensionSizes[0].ConstantValue;
            return sizeConstant is { HasValue: true, Value: int size } ? size : null;
        }

        // A jagged `object[][]` or a single-row `object[]`: count the initializer's elements.
        return arrayCreation.Initializer?.ElementValues.Length;
    }
}

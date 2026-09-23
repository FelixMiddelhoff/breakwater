using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Breakwater.Analyzers.Configuration;
using Breakwater.Analyzers.Operations;
using Breakwater.Analyzers.Rules;
using Breakwater.Analyzers.Suppression;
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
    private const string MigrationMetadataName = "Microsoft.EntityFrameworkCore.Migrations.Migration";
    private const string MigrationAttributeMetadataName = "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute";

    /// <summary>
    /// One <see cref="MigrationContext"/> per <c>Up</c> method, built the first time any of its
    /// operations is analyzed and reused by the rest: rules that need to know which tables were
    /// created earlier in the same migration do not each rescan the method.
    /// </summary>
    private static readonly ConditionalWeakTable<SyntaxNode, MigrationContext> ContextCache = new();

    /// <summary>BW999: the analyzer must never crash a build; a rule failure becomes a low-severity diagnostic instead.</summary>
    internal static readonly DiagnosticDescriptor Bw999Descriptor = new(
        "BW999",
        "Breakwater failed to analyze an operation",
        "Breakwater analyzer failed on '{0}' ({1}); this operation was not checked",
        "Migration",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        "A rule threw while analyzing this call. This is a Breakwater bug, not a problem with the migration; please report it.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        RuleCatalog.All.Select(rule => rule.Descriptor)
            .Concat(new[]
            {
                Bw999Descriptor,
                DiscoverabilityRules.MissingAttributeDescriptor,
                DiscoverabilityRules.DuplicateIdDescriptor,
                DiscoverabilityRules.EnsureCreatedDescriptor,
                DownEmptyRule.Descriptor,
            })
            .ToImmutableArray();

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

        // Read once: none of the breakwater_* global options can change mid-compilation.
        var configuration = BreakwaterConfiguration.Read(context.Compilation, context.Options.AnalyzerConfigOptionsProvider);
        var profile = configuration.Profile;
        var migrationAttributeType = context.Compilation.GetTypeByMetadataName(MigrationAttributeMetadataName);

        context.RegisterOperationAction(
            operationContext => AnalyzeInvocation(operationContext, migrationBuilder, configuration, migrationAttributeType),
            OperationKind.Invocation);

        var migrationType = context.Compilation.GetTypeByMetadataName(MigrationMetadataName);
        if (migrationType is not null)
        {
            DiscoverabilityRules.Register(context, migrationType, migrationAttributeType);
            DownEmptyRule.Register(context, migrationType, profile);
        }
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol migrationBuilder,
        BreakwaterConfiguration configuration,
        INamedTypeSymbol? migrationAttributeType)
    {
        try
        {
            AnalyzeInvocationCore(context, migrationBuilder, configuration, migrationAttributeType);
        }
        catch (System.Exception ex)
        {
            var syntax = context.Operation.Syntax;
            context.ReportDiagnostic(Diagnostic.Create(
                Bw999Descriptor,
                syntax.GetLocation(),
                syntax.ToString().Length > 60 ? syntax.ToString().Substring(0, 60) + "..." : syntax.ToString(),
                ex.GetType().Name));
        }
    }

    private static void AnalyzeInvocationCore(
        OperationAnalysisContext context,
        INamedTypeSymbol migrationBuilder,
        BreakwaterConfiguration configuration,
        INamedTypeSymbol? migrationAttributeType)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var enclosingMethod = invocation.Syntax.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (enclosingMethod?.Identifier.ValueText == "Down")
        {
            return;
        }

        // breakwater_since_migration: skip migrations at or before the configured id entirely -
        // the adoption path for existing repos with production migration history. Migration ids
        // are EF's timestamp-prefixed strings ("20260115120000_InitialCreate"); string comparison
        // matches EF's own discovery/apply ordering. An unreadable/missing id is never skipped.
        if (configuration.SinceMigrationId is { } since
            && GetEnclosingMigrationId(invocation, migrationAttributeType) is { } migrationId
            && string.CompareOrdinal(migrationId, since) <= 0)
        {
            return;
        }

        var operation = MigrationOperationReader.Read(invocation, migrationBuilder, configuration.Provider);
        if (operation is null)
        {
            return;
        }

        var migrationContext = enclosingMethod is null
            ? new MigrationContext(new HashSet<string>())
            : ContextCache.GetValue(enclosingMethod, node => BuildContext(node, invocation.SemanticModel!, migrationBuilder, configuration.Provider));

        var results = new Dictionary<string, object[]>();
        foreach (var rule in RuleCatalog.All)
        {
            object[]? messageArguments;
            try
            {
                messageArguments = rule.Check(operation, migrationContext);
            }
            catch (System.Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(Bw999Descriptor, operation.Location, rule.Descriptor.Id, ex.GetType().Name));
                continue;
            }

            if (messageArguments is not null)
            {
                results[rule.Descriptor.Id] = messageArguments;
            }
        }

        // Most-specific-wins: a rule in RuleCatalog.Suppresses removes the ids it suppresses
        // from the result set when it also fired.
        foreach (var entry in RuleCatalog.Suppresses)
        {
            if (results.ContainsKey(entry.Key))
            {
                foreach (var loser in entry.Value)
                {
                    results.Remove(loser);
                }
            }
        }

        // BW004/BW005/BW015/BW026 share one AlterColumn call; only the first in catalog order stays.
        var alterColumnHit = RuleCatalog.AlterColumnExclusiveGroup.FirstOrDefault(id => results.ContainsKey(id));
        if (alterColumnHit is not null)
        {
            foreach (var id in RuleCatalog.AlterColumnExclusiveGroup)
            {
                if (id != alterColumnHit)
                {
                    results.Remove(id);
                }
            }
        }

        // BW022 is advisory and only appears when nothing more specific fired for this operation.
        if (results.Count > 1 && results.ContainsKey("BW022"))
        {
            results.Remove("BW022");
        }

        foreach (var rule in RuleCatalog.All)
        {
            if (!results.TryGetValue(rule.Descriptor.Id, out var messageArguments))
            {
                continue;
            }

            // "Off (strict)" tier: silent under the default recommended profile, reported under strict.
            if (BreakwaterProfileReader.StrictOnlyRuleIds.Contains(rule.Descriptor.Id) && configuration.Profile != BreakwaterProfile.Strict)
            {
                continue;
            }

            var suppression = SuppressionComment.Check(invocation.Syntax, rule.Descriptor.Id);
            if (suppression == SuppressionComment.Result.SuppressedWithReason)
            {
                continue;
            }

            var descriptor = suppression == SuppressionComment.Result.ReasonRequired
                ? ReasonRequiredDescriptor(rule.Descriptor)
                : rule.Descriptor;
            descriptor = ApplySeverityDowngrades(descriptor, rule.Descriptor.Id, operation, configuration);
            context.ReportDiagnostic(Diagnostic.Create(descriptor, operation.Location, messageArguments));
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DiagnosticDescriptor> ReasonRequiredDescriptors = new();

    /// <summary>
    /// A <c>// breakwater: allow BWxxx</c> comment with no reason does not suppress: the original
    /// diagnostic stays and its message says a reason is required, per
    /// <c>breakwater-rules.md</c>'s configuration section.
    /// </summary>
    private static DiagnosticDescriptor ReasonRequiredDescriptor(DiagnosticDescriptor original)
    {
        return ReasonRequiredDescriptors.GetOrAdd(original.Id, _ => new DiagnosticDescriptor(
            original.Id,
            original.Title,
            original.MessageFormat + " (suppression comment ignored: requires a non-empty reason)",
            original.Category,
            original.DefaultSeverity,
            original.IsEnabledByDefault,
            original.Description,
            original.HelpLinkUri,
            original.CustomTags.ToArray()));
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DiagnosticDescriptor> InfoSeverityDescriptors = new();

    /// <summary>
    /// Downgrades a diagnostic to Info when <c>breakwater_deploy_model = downtime_ok</c> softens
    /// this rule id, or when the operation's table is listed in <c>breakwater_small_tables</c> and
    /// this rule id is one of the table-lock/lock-duration rules that setting softens. Only ever
    /// downgrades (never raises) the severity, and never changes the descriptor when neither
    /// condition applies.
    /// </summary>
    private static DiagnosticDescriptor ApplySeverityDowngrades(
        DiagnosticDescriptor descriptor,
        string ruleId,
        MigrationOperation operation,
        BreakwaterConfiguration configuration)
    {
        var deployModelSoftens = configuration.DeployModel == BreakwaterDeployModel.DowntimeOk
            && BreakwaterConfiguration.DeployModelSensitiveRuleIds.Contains(ruleId);

        var smallTableSoftens = BreakwaterConfiguration.SmallTableSensitiveRuleIds.Contains(ruleId)
            && (configuration.SmallTables.Contains(operation.Table) || configuration.SmallTables.Contains(operation.QualifiedTable));

        if (!deployModelSoftens && !smallTableSoftens)
        {
            return descriptor;
        }

        if (descriptor.DefaultSeverity == DiagnosticSeverity.Info)
        {
            return descriptor;
        }

        return InfoSeverityDescriptors.GetOrAdd(descriptor.Id + ":" + descriptor.MessageFormat, _ => new DiagnosticDescriptor(
            descriptor.Id,
            descriptor.Title,
            descriptor.MessageFormat,
            descriptor.Category,
            DiagnosticSeverity.Info,
            descriptor.IsEnabledByDefault,
            descriptor.Description,
            descriptor.HelpLinkUri,
            descriptor.CustomTags.ToArray()));
    }

    /// <summary>
    /// The <c>[Migration("id")]</c> attribute's id for the class this invocation lives in, the
    /// same attribute <see cref="DiscoverabilityRules"/> reads for BW028. Null when the enclosing
    /// class, its attribute, or the attribute's constant constructor argument cannot be found -
    /// <c>breakwater_since_migration</c> never skips a migration it cannot identify.
    /// </summary>
    private static string? GetEnclosingMigrationId(IInvocationOperation invocation, INamedTypeSymbol? migrationAttributeType)
    {
        var classDeclaration = invocation.Syntax.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null || invocation.SemanticModel is null)
        {
            return null;
        }

        var typeSymbol = invocation.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (typeSymbol is null)
        {
            return null;
        }

        var attribute = typeSymbol.GetAttributes().FirstOrDefault(a =>
            migrationAttributeType is not null
                ? SymbolEqualityComparer.Default.Equals(a.AttributeClass, migrationAttributeType)
                : a.AttributeClass?.Name == "MigrationAttribute");

        if (attribute is null || attribute.ConstructorArguments.Length == 0)
        {
            return null;
        }

        return attribute.ConstructorArguments[0].Value as string;
    }

    /// <summary>
    /// Scans one <c>Up</c> method (or local function/lambda inside it - those are not separate
    /// migration scopes) for <c>CreateTable</c> calls, so later rules know which tables are still
    /// empty. Runs once per method: the result is cached by <see cref="ContextCache"/>.
    /// </summary>
    private static readonly HashSet<MigrationOperationKind> NonRiskyKinds = new()
    {
        MigrationOperationKind.CreateTable,
        MigrationOperationKind.Sql,
        MigrationOperationKind.InsertData,
    };

    private static readonly HashSet<MigrationOperationKind> SchemaKinds = new()
    {
        MigrationOperationKind.DropColumn, MigrationOperationKind.DropTable, MigrationOperationKind.RenameColumn,
        MigrationOperationKind.RenameTable, MigrationOperationKind.AlterColumn, MigrationOperationKind.AddColumn,
        MigrationOperationKind.CreateTable, MigrationOperationKind.CreateIndex, MigrationOperationKind.AddForeignKey,
        MigrationOperationKind.AddCheckConstraint, MigrationOperationKind.AddUniqueConstraint,
        MigrationOperationKind.AddPrimaryKey, MigrationOperationKind.DropPrimaryKey,
        MigrationOperationKind.DropUniqueConstraint, MigrationOperationKind.DropCheckConstraint,
        MigrationOperationKind.DropForeignKey, MigrationOperationKind.DropSchema, MigrationOperationKind.DropSequence,
        MigrationOperationKind.AlterSequence, MigrationOperationKind.AlterDatabase,
    };

    private static readonly HashSet<MigrationOperationKind> DataKinds = new()
    {
        MigrationOperationKind.InsertData, MigrationOperationKind.UpdateData, MigrationOperationKind.DeleteData,
    };

    private static MigrationContext BuildContext(SyntaxNode methodNode, SemanticModel semanticModel, INamedTypeSymbol migrationBuilder, BreakwaterDatabaseProvider providerOverride)
    {
        var tablesCreated = new HashSet<string>();
        var tablesWithDropColumn = new HashSet<string>();
        var tablesWithAddColumn = new HashSet<string>();
        var placeholderDefaultColumns = new HashSet<string>();
        var nullableAddedColumns = new HashSet<string>();
        var riskyCountByTable = new Dictionary<string, int>();
        var lastRiskyLocationByTable = new Dictionary<string, Location>();
        var hasSchemaOperation = false;
        var hasDataOperation = false;
        var hasLockTimeoutStatement = false;
        var operationCount = 0;
        Location? firstOperationLocation = null;

        // First pass: every table CreateTable'd in this method, so the second pass below can
        // tell (regardless of call order) that a later CreateIndex/AddForeignKey/etc. on that
        // table still targets an empty table. Without this, BW022 (and only BW022 - every other
        // rule already gets this for free by running its own single pass after the whole
        // MigrationContext is built) miscounted CreateIndex/AddForeignKey calls that immediately
        // follow a CreateTable for the same table as "risky", which fired constantly on ordinary
        // Initial migrations (a table created and indexed in the same migration has no existing
        // data to lock or violate) - see breakwater-quality-policy.md principle 4.
        foreach (var invocationSyntax in methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetOperation(invocationSyntax) is IInvocationOperation createTableCandidate &&
                MigrationOperationReader.Read(createTableCandidate, migrationBuilder, providerOverride) is { Kind: MigrationOperationKind.CreateTable } createTableOp)
            {
                tablesCreated.Add(createTableOp.QualifiedTable);
            }
        }

        foreach (var invocationSyntax in methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetOperation(invocationSyntax) is not IInvocationOperation invocationOperation)
            {
                continue;
            }

            var operation = MigrationOperationReader.Read(invocationOperation, migrationBuilder, providerOverride);
            if (operation is null)
            {
                continue;
            }

            operationCount++;
            firstOperationLocation ??= operation.Location;

            if (operation.Kind == MigrationOperationKind.DropColumn)
            {
                tablesWithDropColumn.Add(operation.QualifiedTable);
            }

            if (operation.Kind == MigrationOperationKind.AddColumn)
            {
                tablesWithAddColumn.Add(operation.QualifiedTable);
                if (operation.HasPlaceholderDefaultValue && operation.Column is not null)
                {
                    placeholderDefaultColumns.Add(operation.QualifiedTable + "." + operation.Column);
                }

                if (operation.Nullable && operation.Column is not null)
                {
                    nullableAddedColumns.Add(operation.QualifiedTable + "." + operation.Column);
                }
            }

            if (operation.Kind == MigrationOperationKind.Sql && operation.SqlText?.IndexOf("lock_timeout", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hasLockTimeoutStatement = true;
            }

            if (SchemaKinds.Contains(operation.Kind))
            {
                hasSchemaOperation = true;
            }

            if (DataKinds.Contains(operation.Kind))
            {
                hasDataOperation = true;
            }

            if (!NonRiskyKinds.Contains(operation.Kind) && !tablesCreated.Contains(operation.QualifiedTable))
            {
                riskyCountByTable.TryGetValue(operation.QualifiedTable, out var existingCount);
                riskyCountByTable[operation.QualifiedTable] = existingCount + 1;
                lastRiskyLocationByTable[operation.QualifiedTable] = operation.Location;
            }
        }

        var suppressTransaction = HasSuppressTransactionAssignment(methodNode, semanticModel, migrationBuilder);
        return new MigrationContext(
            tablesCreated,
            suppressTransaction,
            tablesWithDropColumn,
            tablesWithAddColumn,
            placeholderDefaultColumns,
            hasSchemaOperation && hasDataOperation,
            operationCount,
            firstOperationLocation,
            riskyCountByTable,
            lastRiskyLocationByTable,
            nullableAddedColumns,
            hasLockTimeoutStatement);
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

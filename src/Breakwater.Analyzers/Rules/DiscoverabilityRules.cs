using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW028 and BW032 look outside a migration's <c>Up</c> method by design (per the noise policy's
/// exception to "never analyze what is not a migration Up"), so they do not fit the per-operation
/// <see cref="IMigrationRule"/> shape the rest of the catalog uses and are wired up directly here.
/// </summary>
internal static class DiscoverabilityRules
{
    public static readonly DiagnosticDescriptor MissingAttributeDescriptor = RuleDescriptors.Create(
        "BW028",
        "Migration class is missing [Migration(\"id\")] or has a duplicate id",
        "Migration class '{0}' has no [Migration(\"id\")] attribute; EF Core will not discover it",
        "EF Core finds migrations by this attribute. A class without it (for example after deleting " +
        "the generated .Designer.cs file) is silently skipped, and the schema quietly diverges between " +
        "environments that do and do not run it.");

    /// <summary>Same rule id and text as <see cref="MissingAttributeDescriptor"/>; a different message shape for the duplicate-id case.</summary>
    public static readonly DiagnosticDescriptor DuplicateIdDescriptor = new(
        "BW028",
        MissingAttributeDescriptor.Title,
        "Migration id '{0}' is used by more than one migration class in this compilation",
        MissingAttributeDescriptor.Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        "EF Core applies migrations by id order; two classes with the same id mean only one of them " +
        "ever really runs, and which one depends on discovery order, not on which you intended.",
        helpLinkUri: MissingAttributeDescriptor.HelpLinkUri,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor EnsureCreatedDescriptor = new(
        "BW032",
        "EnsureCreated() is used in a project that also has migrations",
        "Database.EnsureCreated() skips the migration history; migrations in this project will fail or be skipped for a database it creates",
        "Migration",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        "EnsureCreated() builds the schema straight from the model and never records applied migrations. " +
        "Any later call to Database.Migrate() on that database either fails or silently does nothing. Use " +
        "Database.Migrate() (or apply migrations through the EF Core CLI/tooling) instead, even for the " +
        "first deploy.",
        helpLinkUri: "https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/BW032.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol migrationType, INamedTypeSymbol? migrationAttributeType)
    {
        var idLocations = new ConcurrentDictionary<string, ConcurrentBag<Location>>();
        var ensureCreatedCalls = new ConcurrentBag<Location>();
        var hasMigrations = new bool[1];

        context.RegisterSymbolAction(
            symbolContext => AnalyzeType((INamedTypeSymbol)symbolContext.Symbol, migrationType, migrationAttributeType, symbolContext, idLocations, hasMigrations),
            SymbolKind.NamedType);

        context.RegisterOperationAction(
            operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;
                if (invocation.TargetMethod.Name == "EnsureCreated")
                {
                    ensureCreatedCalls.Add(invocation.Syntax.GetLocation());
                }
            },
            OperationKind.Invocation);

        context.RegisterCompilationEndAction(endContext =>
        {
            foreach (var group in idLocations)
            {
                if (group.Value.Count <= 1)
                {
                    continue;
                }

                foreach (var location in group.Value)
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(DuplicateIdDescriptor, location, group.Key));
                }
            }

            if (!hasMigrations[0])
            {
                return;
            }

            foreach (var location in ensureCreatedCalls)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(EnsureCreatedDescriptor, location));
            }
        });
    }

    private static void AnalyzeType(
        INamedTypeSymbol type,
        INamedTypeSymbol migrationType,
        INamedTypeSymbol? migrationAttributeType,
        SymbolAnalysisContext context,
        ConcurrentDictionary<string, ConcurrentBag<Location>> idLocations,
        bool[] hasMigrations)
    {
        if (!InheritsFrom(type, migrationType))
        {
            return;
        }

        hasMigrations[0] = true;

        // An abstract base migration class is a shared base, not a concrete migration EF must
        // discover: it is expected to have no [Migration] attribute of its own.
        if (type.IsAbstract)
        {
            return;
        }

        var attribute = type.GetAttributes().FirstOrDefault(a =>
            migrationAttributeType is not null
                ? SymbolEqualityComparer.Default.Equals(a.AttributeClass, migrationAttributeType)
                : a.AttributeClass?.Name == "MigrationAttribute");

        if (attribute is null)
        {
            var location = type.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(MissingAttributeDescriptor, location, type.Name));
            return;
        }

        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string id)
        {
            var location = type.Locations.FirstOrDefault() ?? Location.None;
            idLocations.GetOrAdd(id, _ => new ConcurrentBag<Location>()).Add(location);
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}

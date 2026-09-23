using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW007: building an index on an existing table blocks writes for the duration, unless the
/// provider's online/concurrent build is opted into.
/// </summary>
internal sealed class CreateIndexOnlineRule : IMigrationRule
{
    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW007",
        "CreateIndex without an online/concurrent build",
        "CreateIndex '{0}' on '{1}' blocks writes while the index builds",
        "A plain index build takes a lock that blocks writes to the table for as long as the build " +
        "takes, which on a large table can be minutes or hours. On PostgreSQL, chain " +
        "'.Annotation(\"Npgsql:CreatedConcurrently\", true)' and set 'migrationBuilder.SuppressTransaction " +
        "= true;' in the same method. On SQL Server (Enterprise/Developer editions), chain " +
        "'.Annotation(\"SqlServer:Online\", true)'.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.CreateIndex)
        {
            return null;
        }

        if (context.IsTableCreatedInThisMigration(operation.QualifiedTable))
        {
            return null;
        }

        var isConcurrentOnPostgres = operation.IsCreatedConcurrently && context.SuppressTransaction;
        if (isConcurrentOnPostgres || operation.IsOnline)
        {
            return null;
        }

        return new object[] { operation.Column!, operation.QualifiedTable };
    }
}

using System.Text.RegularExpressions;
using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>
/// BW034: raw SQL that disables an integrity check (<c>NOCHECK CONSTRAINT</c>, <c>DISABLE
/// TRIGGER</c>, <c>SET FOREIGN_KEY_CHECKS = 0</c>) or drops the whole database lets data become
/// invalid, or destroys it, without anyone noticing until later.
/// </summary>
internal sealed class SqlDisablesSafetyRule : IMigrationRule
{
    private static readonly Regex NoCheckConstraint = new(@"NOCHECK\s+CONSTRAINT", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DisableTrigger = new(@"DISABLE\s+TRIGGER", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ForeignKeyChecksOff = new(@"FOREIGN_KEY_CHECKS\s*=\s*0", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropDatabase = new(@"DROP\s+DATABASE", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DiagnosticDescriptor Descriptor { get; } = RuleDescriptors.Create(
        "BW034",
        "Raw SQL disables an integrity check or drops the database",
        "Sql(...) contains '{0}', which lets data become invalid (or destroys it) without anyone noticing",
        "Turning off constraint or trigger enforcement, or disabling foreign key checks, means rows can " +
        "be written that violate the rules the schema is supposed to guarantee - and nothing reports it " +
        "until something downstream breaks. Re-enable the check immediately after, in the same migration, " +
        "or avoid disabling it at all.");

    public object[]? Check(MigrationOperation operation, MigrationContext context)
    {
        if (operation.Kind != MigrationOperationKind.Sql || operation.SqlText is not { } sql)
        {
            return null;
        }

        if (DropDatabase.IsMatch(sql))
        {
            return new object[] { "DROP DATABASE" };
        }

        if (NoCheckConstraint.IsMatch(sql))
        {
            return new object[] { "NOCHECK CONSTRAINT" };
        }

        if (DisableTrigger.IsMatch(sql))
        {
            return new object[] { "DISABLE TRIGGER" };
        }

        if (ForeignKeyChecksOff.IsMatch(sql))
        {
            return new object[] { "SET FOREIGN_KEY_CHECKS = 0" };
        }

        return null;
    }
}

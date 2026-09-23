using Breakwater.Analyzers.Operations;
using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>A check that looks at one migration operation at a time.</summary>
internal interface IMigrationRule
{
    DiagnosticDescriptor Descriptor { get; }

    /// <summary>
    /// Returns the message arguments for the diagnostic when the operation is unsafe,
    /// or null when the rule has nothing to say about it.
    /// </summary>
    object[]? Check(MigrationOperation operation, MigrationContext context);
}

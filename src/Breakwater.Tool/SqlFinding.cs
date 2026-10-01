using Microsoft.CodeAnalysis;

namespace Breakwater.Tool;

/// <summary>
/// One risky shape found in a generated script, ready for console or JSON output. <see
/// cref="Severity"/> reuses the same <see cref="DiagnosticSeverity"/> the analyzer's own
/// <c>RuleDescriptors.Create(...)</c> calls assign each rule (see <c>Breakwater.Analyzers.Rules.
/// RuleDescriptors</c>) rather than inventing a parallel severity concept here.
/// </summary>
internal sealed record SqlFinding(string RuleId, DiagnosticSeverity Severity, string Migration, int Line, string Message);

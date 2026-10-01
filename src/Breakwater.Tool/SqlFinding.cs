namespace Breakwater.Tool;

/// <summary>One risky shape found in a generated script, ready for console or JSON output.</summary>
internal sealed record SqlFinding(string RuleId, string Severity, string Migration, int Line, string Message);

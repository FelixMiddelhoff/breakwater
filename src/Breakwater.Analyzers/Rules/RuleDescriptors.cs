using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>Builds diagnostic descriptors with the settings every Breakwater rule shares.</summary>
internal static class RuleDescriptors
{
    private const string Category = "Migration";
    private const string DocsRoot = "https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/";

    public static DiagnosticDescriptor Create(
        string id,
        string title,
        string messageFormat,
        string description,
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        bool isEnabledByDefault = true)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            Category,
            severity,
            isEnabledByDefault,
            description,
            helpLinkUri: DocsRoot + id + ".md");
    }

    /// <summary>Suggestion tier (recommended profile, IDE hint only): Info severity, enabled by default.</summary>
    public const DiagnosticSeverity Suggestion = DiagnosticSeverity.Info;

    /// <summary>Off (strict profile only): disabled by default; a profile can re-enable it.</summary>
    public const bool StrictOnly = false;
}

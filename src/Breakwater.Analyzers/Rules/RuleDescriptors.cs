using Microsoft.CodeAnalysis;

namespace Breakwater.Analyzers.Rules;

/// <summary>Builds diagnostic descriptors with the settings every Breakwater rule shares.</summary>
internal static class RuleDescriptors
{
    private const string Category = "Migration";
    private const string DocsRoot = "https://github.com/FelixMiddelhoff/breakwater/blob/main/docs/rules/";

    public static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description,
            helpLinkUri: DocsRoot + id + ".md");
    }
}

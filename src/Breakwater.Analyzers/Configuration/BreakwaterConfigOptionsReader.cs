using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Configuration;

/// <summary>
/// Shared read path for all <c>breakwater_*</c> plain <c>.editorconfig</c> keys (not the
/// <c>dotnet_diagnostic.*</c> severity family, which the compiler already handles natively).
/// </summary>
/// <remarks>
/// A real project <c>.editorconfig</c> (the documented, expected mechanism per breakwater-rules.md
/// - "all via <c>.editorconfig</c> / MSBuild property, no config file") does NOT populate
/// <see cref="AnalyzerConfigOptionsProvider.GlobalOptions"/> for custom keys the way a
/// <c>.globalconfig</c> file (<c>is_global = true</c>) does. Custom keys from an ordinary
/// <c>.editorconfig</c> section (<c>[*.cs]</c>, <c>[*]</c>, or unsectioned under <c>root = true</c>)
/// surface only through <see cref="AnalyzerConfigOptionsProvider.GetOptions(Microsoft.CodeAnalysis.SyntaxTree)"/>
/// - the per-syntax-tree options the compiler already resolves via normal EditorConfig cascading.
/// Since <c>breakwater_*</c> settings are project-wide, not per-file, this reads the first syntax
/// tree in the compilation that defines the key (all trees under one project-root
/// <c>.editorconfig</c> agree in practice) and falls back to <c>GlobalOptions</c> for setups that
/// do use a <c>.globalconfig</c> (some CI/build pipelines).
/// </remarks>
internal static class BreakwaterConfigOptionsReader
{
    public static bool TryGetValue(Compilation compilation, AnalyzerConfigOptionsProvider provider, string key, out string? value)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var treeOptions = provider.GetOptions(tree);
            if (treeOptions.TryGetValue(key, out var treeValue))
            {
                value = treeValue;
                return true;
            }
        }

        if (provider.GlobalOptions.TryGetValue(key, out var globalValue))
        {
            value = globalValue;
            return true;
        }

        value = null;
        return false;
    }
}

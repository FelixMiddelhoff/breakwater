using System;
using Breakwater.Analyzers.Configuration;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Suppression;

/// <summary>
/// <c>breakwater_profile</c> from <c>.editorconfig</c> (global config, a plain key - not the
/// <c>dotnet_diagnostic.*</c> per-rule severity family). Default <c>recommended</c>; <c>strict</c>
/// enables the rules the quality policy lists as "Off (strict)": BW011, BW020, BW030.
/// </summary>
internal enum BreakwaterProfile
{
    Recommended,
    Strict,
}

internal static class BreakwaterProfileReader
{
    private const string Key = "breakwater_profile";

    /// <summary>
    /// Reads the profile from the compilation's <c>.editorconfig</c> options. Read once per
    /// compilation start and passed down, rather than re-read per operation - the value cannot
    /// change mid-compilation. See <see cref="BreakwaterConfigOptionsReader"/> for why this checks
    /// per-syntax-tree options (real project <c>.editorconfig</c> files) before falling back to
    /// <see cref="AnalyzerConfigOptionsProvider.GlobalOptions"/> (<c>.globalconfig</c>-style setups).
    /// </summary>
    public static BreakwaterProfile Read(Microsoft.CodeAnalysis.Compilation compilation, AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (BreakwaterConfigOptionsReader.TryGetValue(compilation, optionsProvider, Key, out var value)
            && string.Equals(value?.Trim(), "strict", StringComparison.OrdinalIgnoreCase))
        {
            return BreakwaterProfile.Strict;
        }

        return BreakwaterProfile.Recommended;
    }

    /// <summary>Rule ids the quality policy lists as "Off (strict)": silent under <c>recommended</c>, reported under <c>strict</c>.</summary>
    public static readonly System.Collections.Generic.HashSet<string> StrictOnlyRuleIds = new() { "BW011", "BW020", "BW030" };
}

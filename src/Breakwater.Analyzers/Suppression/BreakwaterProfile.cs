using System;
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
    /// Reads the profile from the compilation's global <c>.editorconfig</c> options. Read once per
    /// compilation start and passed down, rather than re-read per operation - the value cannot
    /// change mid-compilation.
    /// </summary>
    public static BreakwaterProfile Read(AnalyzerConfigOptionsProvider optionsProvider)
    {
        if (optionsProvider.GlobalOptions.TryGetValue(Key, out var value)
            && string.Equals(value?.Trim(), "strict", StringComparison.OrdinalIgnoreCase))
        {
            return BreakwaterProfile.Strict;
        }

        return BreakwaterProfile.Recommended;
    }

    /// <summary>Rule ids the quality policy lists as "Off (strict)": silent under <c>recommended</c>, reported under <c>strict</c>.</summary>
    public static readonly System.Collections.Generic.HashSet<string> StrictOnlyRuleIds = new() { "BW011", "BW020", "BW030" };
}

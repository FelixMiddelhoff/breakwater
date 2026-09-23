using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Tests.Support;

/// <summary>A fixed set of global <c>.editorconfig</c> options for a test compilation - just enough
/// for <c>breakwater_profile</c>; per-tree and additional-file options are not needed by any test.</summary>
internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    public static readonly TestAnalyzerConfigOptions Empty = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> values;

    public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
    }

    public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; }

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
    {
        GlobalOptions = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.SyntaxTree tree) => GlobalOptions;

    public override AnalyzerConfigOptions GetOptions(Microsoft.CodeAnalysis.AdditionalText textFile) => GlobalOptions;
}

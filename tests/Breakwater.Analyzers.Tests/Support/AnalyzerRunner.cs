using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Breakwater.Analyzers.Tests.Support;

/// <summary>Compiles a C# snippet next to the EF Core stubs and runs the analyzer on it.</summary>
internal static class AnalyzerRunner
{
    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    /// <summary>
    /// Runs the analyzer over <paramref name="source"/> and returns only Breakwater diagnostics,
    /// ordered by position. The snippet must compile without errors.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        bool includeEfCoreStubs = true,
        System.Collections.Generic.IReadOnlyDictionary<string, string>? globalOptions = null)
    {
        var trees = includeEfCoreStubs
            ? new[] { Parse(EfCoreStubs.Source), Parse(source) }
            : new[] { Parse(source) };

        var compilation = CSharpCompilation.Create(
            "MigrationsUnderTest",
            trees,
            FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Count > 0)
        {
            throw new InvalidOperationException("Test snippet does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, compileErrors));
        }

        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(globalOptions ?? new System.Collections.Generic.Dictionary<string, string>()));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MigrationAnalyzer()), analyzerOptions);
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        var crash = diagnostics.FirstOrDefault(d => d.Id == "AD0001");
        if (crash is not null)
        {
            throw new InvalidOperationException("Analyzer crashed: " + crash.GetMessage());
        }

        return diagnostics
            .Where(d => d.Id.StartsWith("BW", StringComparison.Ordinal))
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToImmutableArray();
    }

    private static SyntaxTree Parse(string source) => CSharpSyntaxTree.ParseText(source);

    /// <summary>The runtime running the tests knows where its reference assemblies are.</summary>
    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The runtime did not report its platform assemblies.");
        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}

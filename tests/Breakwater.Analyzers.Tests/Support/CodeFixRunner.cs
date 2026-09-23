using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Breakwater.Analyzers.Tests.Support;

/// <summary>
/// Runs a single <see cref="CodeFixProvider"/> against a compiled snippet, the same shape
/// <see cref="AnalyzerRunner"/> uses for rules: compile the snippet plus the EF Core stubs, run the
/// real analyzer to get a diagnostic, hand it to the fix provider, apply the chosen code action, and
/// return the resulting source text. No <c>Microsoft.CodeAnalysis.Testing</c> package is referenced
/// (the project keeps runtime/shipped dependencies at zero and this is small enough to hand-roll);
/// this uses a minimal <see cref="AdhocWorkspace"/> only to host the <see cref="Document"/> a
/// <see cref="CodeFixContext"/> requires.
/// </summary>
internal static class CodeFixRunner
{
    /// <summary>
    /// Applies the code action whose <see cref="CodeAction.EquivalenceKey"/> matches
    /// <paramref name="equivalenceKey"/> (or the only registered action, when there is exactly one
    /// and no key is given) and returns the fixed source.
    /// </summary>
    public static async Task<string> ApplyFixAsync(
        string source,
        CodeFixProvider provider,
        string diagnosticId,
        string? equivalenceKey = null)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution.AddProject(projectId, "MigrationsUnderTest", "MigrationsUnderTest", LanguageNames.CSharp);

        var stubsId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(stubsId, "EfCoreStubs.cs", SourceText.From(EfCoreStubs.Source));

        var snippetId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(snippetId, "Snippet.cs", SourceText.From(source));

        solution = solution.AddMetadataReferences(projectId, AnalyzerRunner.FrameworkReferences);
        solution = solution.WithProjectCompilationOptions(
            projectId,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync())!;

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Count > 0)
        {
            throw new InvalidOperationException("Test snippet does not compile:" + Environment.NewLine + string.Join(Environment.NewLine, compileErrors));
        }

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MigrationAnalyzer()));
        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.FirstOrDefault(d => d.Id == diagnosticId)
            ?? throw new InvalidOperationException($"No {diagnosticId} diagnostic was produced by the snippet.");

        var document = project.GetDocument(snippetId)!;

        CodeAction? chosenAction = null;
        var context = new CodeFixContext(document, diagnostic, (action, _) =>
        {
            if (equivalenceKey is null || action.EquivalenceKey == equivalenceKey)
            {
                chosenAction ??= action;
            }
        }, default);

        await provider.RegisterCodeFixesAsync(context);

        if (chosenAction is null)
        {
            throw new InvalidOperationException($"No matching code action was registered for {diagnosticId} (equivalenceKey: {equivalenceKey ?? "<any>"}).");
        }

        var operations = await chosenAction.GetOperationsAsync(default);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        var newDocument = applyOperation.ChangedSolution.GetDocument(snippetId)!;
        var newText = await newDocument.GetTextAsync();
        return newText.ToString();
    }
}

using System.Threading.Tasks;
using Breakwater.Analyzers.CodeFixes;
using Breakwater.Analyzers.Tests.Support;
using Xunit;

namespace Breakwater.Analyzers.Tests.CodeFixes;

/// <summary>BW032's code fix: remove a bare-statement <c>EnsureCreated()</c> call.</summary>
public class EnsureCreatedCodeFixProviderTests
{
    private const string Source = """
        using Microsoft.EntityFrameworkCore.Migrations;
        using Microsoft.EntityFrameworkCore.Infrastructure;

        [Migration("20260101000000_AddThings")]
        public class AddThings : Migration
        {
            protected override void Up(MigrationBuilder migrationBuilder) { }
        }

        public class Startup
        {
            public void Configure(DatabaseFacade database)
            {
                database.EnsureCreated();
            }
        }
        """;

    [Fact]
    public async Task Removes_the_bare_statement_call()
    {
        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            Source, new EnsureCreatedCodeFixProvider(), "BW032", "BW032_Remove");

        Assert.DoesNotContain("EnsureCreated", fixedSource);
    }

    [Fact]
    public async Task Fix_result_no_longer_triggers_BW032()
    {
        var fixedSource = await CodeFixRunner.ApplyFixAsync(
            Source, new EnsureCreatedCodeFixProvider(), "BW032", "BW032_Remove");

        var diagnostics = await AnalyzerRunner.AnalyzeAsync(fixedSource);
        Assert.DoesNotContain(diagnostics, d => d.Id == "BW032");
    }

    [Fact]
    public async Task No_fix_is_offered_when_the_return_value_is_used()
    {
        const string sourceWithUsedReturnValue = """
            using Microsoft.EntityFrameworkCore.Migrations;
            using Microsoft.EntityFrameworkCore.Infrastructure;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }
            }

            public class Startup
            {
                public bool Configure(DatabaseFacade database)
                {
                    if (!database.EnsureCreated())
                    {
                        return false;
                    }

                    return true;
                }
            }
            """;

        await Assert.ThrowsAsync<System.InvalidOperationException>(() =>
            CodeFixRunner.ApplyFixAsync(sourceWithUsedReturnValue, new EnsureCreatedCodeFixProvider(), "BW032", "BW032_Remove"));
    }
}

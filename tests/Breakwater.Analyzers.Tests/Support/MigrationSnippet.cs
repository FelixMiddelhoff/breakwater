namespace Breakwater.Analyzers.Tests.Support;

/// <summary>Wraps a few lines of migration code into a complete, compilable migration class.</summary>
internal static class MigrationSnippet
{
    /// <summary>The <paramref name="upBody"/> becomes the body of <c>Up</c>.</summary>
    public static string WithUp(string upBody, string members = "")
    {
        return $$"""
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    {{upBody}}
                }

                {{members}}
            }
            """;
    }

    /// <summary>The <paramref name="downBody"/> becomes the body of <c>Down</c>; <c>Up</c> is empty.</summary>
    public static string WithDown(string downBody)
    {
        return $$"""
            using Microsoft.EntityFrameworkCore.Migrations;

            [Migration("20260101000000_AddThings")]
            public class AddThings : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder) { }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                    {{downBody}}
                }
            }
            """;
    }
}

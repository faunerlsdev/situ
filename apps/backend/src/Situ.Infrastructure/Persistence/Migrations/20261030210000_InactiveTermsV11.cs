using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Situ.Infrastructure.Persistence;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Inserts terms 1.1 as an inactive row. Registration keeps accepting version 2026-10-29
    /// until this row is activated on purpose.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261030210000_InactiveTermsV11")]
    public partial class InactiveTermsV11 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "legal_documents",
                columns: new[] { "id", "country_id", "created_at", "is_active", "locale", "published_at", "type", "updated_at", "url", "version" },
                values: new object[]
                {
                    new Guid("7ad39d3f-9848-4e1a-d5b4-d37731f778ba"),
                    new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"),
                    new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                    false,
                    "es-CO",
                    new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                    "TermsOfUse",
                    null,
                    "https://somossitu.com/terminos",
                    "1.1",
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("7ad39d3f-9848-4e1a-d5b4-d37731f778ba"));
        }
    }
}

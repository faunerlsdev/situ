using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedLegalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "legal_documents",
                columns: new[] { "id", "country_id", "is_active", "locale", "published_at", "type", "url", "version" },
                values: new object[,]
                {
                    { new Guid("0e4e994e-2374-ca09-6474-4fc3cdfaeff2"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "es-CO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TermsOfUse", "https://situ.local/legal/terminos", "2026-01" },
                    { new Guid("86eca7cb-83ce-ad08-5bf9-f7378962a39c"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "es-CO", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PrivacyPolicy", "https://situ.local/legal/privacidad", "2026-01" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("0e4e994e-2374-ca09-6474-4fc3cdfaeff2"));

            migrationBuilder.DeleteData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("86eca7cb-83ce-ad08-5bf9-f7378962a39c"));
        }
    }
}

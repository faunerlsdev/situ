using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLegalPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "key",
                keyValue: "legal.support_url",
                column: "value",
                value: "https://ayuda.somossitu.com/contacto");

            migrationBuilder.UpdateData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("0e4e994e-2374-ca09-6474-4fc3cdfaeff2"),
                columns: new[] { "url", "version" },
                values: new object[] { "https://somossitu.com/terminos", "2026-10-29" });

            migrationBuilder.UpdateData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("86eca7cb-83ce-ad08-5bf9-f7378962a39c"),
                columns: new[] { "url", "version" },
                values: new object[] { "https://somossitu.com/privacidad", "2026-10-29" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("86eca7cb-83ce-ad08-5bf9-f7378962a39c"),
                columns: new[] { "url", "version" },
                values: new object[] { "https://situ.local/legal/privacidad", "2026-01" });

            migrationBuilder.UpdateData(
                table: "legal_documents",
                keyColumn: "id",
                keyValue: new Guid("0e4e994e-2374-ca09-6474-4fc3cdfaeff2"),
                columns: new[] { "url", "version" },
                values: new object[] { "https://situ.local/legal/terminos", "2026-01" });

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "key",
                keyValue: "legal.support_url",
                column: "value",
                value: "https://somossitu.com/soporte");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationResponseCommitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("4907ff6e-2b6b-e914-9bd2-b22d60e80acd"),
                column: "description",
                value: "Términos que no pueden aparecer en el perfil, en un reporte ni en un comentario.");

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[] { new Guid("18634da0-f44e-56b0-4222-534855194abc"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto visible en la app sobre el plazo para atender una denuncia.", "moderation.response_commitment", null, "Revisamos cada denuncia. Si el contenido afecta a un menor, lo ocultamos de inmediato. El resto lo atendemos en menos de 24 horas." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("18634da0-f44e-56b0-4222-534855194abc"));

            migrationBuilder.UpdateData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("4907ff6e-2b6b-e914-9bd2-b22d60e80acd"),
                column: "description",
                value: "Términos que no pueden aparecer en el nombre, el usuario o la bio.");
        }
    }
}

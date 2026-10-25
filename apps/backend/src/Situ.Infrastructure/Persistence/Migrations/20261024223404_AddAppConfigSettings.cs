using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppConfigSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("08ed0c13-9bce-37a2-caf8-561565ee185a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión mínima de la app Android.", "app.android.min_supported_version", null, "0.1.0" },
                    { new Guid("5b779df6-fa32-dfeb-45ca-7520cc2a721c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto del panel inferior cuando se quiere mirar el mapa.", "panel.browse_copy", null, "Quiero chismosear" },
                    { new Guid("6698c5fc-0ddd-a083-70f9-52c967bd1fcc"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Última versión publicada de la app Android.", "app.android.latest_version", null, "0.1.0" },
                    { new Guid("6b02d73e-123d-7c60-9ea8-3df391d52f47"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Última versión publicada de la web.", "app.web.latest_version", null, "0.1.0" },
                    { new Guid("748ad046-c6ec-c36b-13dd-bf25a1e15d0e"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Última versión publicada de la app iOS.", "app.ios.latest_version", null, "0.1.0" },
                    { new Guid("95eb09f1-2333-10d1-5346-36c8f448f29e"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Máximo de caracteres de la descripción de un reporte. El dominio sigue rechazando más de 1000.", "reports.description_max_length", null, "1000" },
                    { new Guid("c57ed59f-80cf-527f-c3a6-c10ae087aafa"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto del panel inferior cuando se quiere publicar.", "panel.report_copy", null, "Quiero reportar algo" },
                    { new Guid("cdd2e3cc-0f04-d20e-1ea3-6ac9beba5d06"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión mínima de la app iOS.", "app.ios.min_supported_version", null, "0.1.0" },
                    { new Guid("e44a2e93-6866-0d14-7630-7619361681f9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión mínima de la web.", "app.web.min_supported_version", null, "0.1.0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("08ed0c13-9bce-37a2-caf8-561565ee185a"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("5b779df6-fa32-dfeb-45ca-7520cc2a721c"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("6698c5fc-0ddd-a083-70f9-52c967bd1fcc"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("6b02d73e-123d-7c60-9ea8-3df391d52f47"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("748ad046-c6ec-c36b-13dd-bf25a1e15d0e"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("95eb09f1-2333-10d1-5346-36c8f448f29e"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("c57ed59f-80cf-527f-c3a6-c10ae087aafa"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("cdd2e3cc-0f04-d20e-1ea3-6ac9beba5d06"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("e44a2e93-6866-0d14-7630-7619361681f9"));
        }
    }
}

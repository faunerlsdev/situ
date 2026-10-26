using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMapZoneNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_zone_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cell_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_zone_notices", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("0b4154cc-3ca4-7311-d446-159617a80473"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Lado de una celda del mapa en tiempo real, en grados.", "map.cell_degrees", null, "0.05" },
                    { new Guid("32f83b44-fb7a-219b-1904-a8df39d07618"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Celdas máximas que una conexión puede seguir a la vez.", "map.max_cells", null, "16" },
                    { new Guid("40f9d4ff-e1aa-e9a9-ff33-349cd6086019"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cambios de suscripción permitidos por conexión en un minuto.", "map.subscribe_calls_per_minute", null, "20" },
                    { new Guid("68f89159-fa14-70ec-a536-91fd563d26b4"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Milisegundos entre tandas de avisos del mapa en tiempo real.", "map.coalesce_interval_milliseconds", null, "1000" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_zone_notices_status_occurred_at",
                table: "map_zone_notices",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_zone_notices");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("0b4154cc-3ca4-7311-d446-159617a80473"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("32f83b44-fb7a-219b-1904-a8df39d07618"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("40f9d4ff-e1aa-e9a9-ff33-349cd6086019"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("68f89159-fa14-70ec-a536-91fd563d26b4"));
        }
    }
}

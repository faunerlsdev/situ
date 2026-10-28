using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveIngest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Point>(
                name: "location",
                table: "live_streams",
                type: "geography(point, 4326)",
                nullable: true,
                oldClrType: typeof(Point),
                oldType: "geography(point, 4326)");

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "live_streams",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "hidden_by_staff",
                table: "live_streams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "hidden_reason",
                table: "live_streams",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_path",
                table: "live_streams",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "publish_key_expires_at",
                table: "live_streams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publish_key_hash",
                table: "live_streams",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "publish_key_used_at",
                table: "live_streams",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recording_path",
                table: "live_streams",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "live_viewer_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_viewer_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_viewer_sessions_live_stream_id",
                        column: x => x.live_stream_id,
                        principalTable: "live_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("2c874b2f-9844-8461-9122-0f4519708016"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Si es false, un menor no puede transmitir en vivo (ni a amigos ni en público). Pendiente de revisión jurídica (Ley 1581 de 2012). No lo enciendas sin ese concepto.", "live.minors_can_stream", null, "false" },
                    { new Guid("35b463b4-9326-13a2-69ad-b1bf4404391d"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Transmisiones simultáneas en todo el servidor. El equipo tiene 2 vCPU y no transcodifica.", "live.max_concurrent", null, "2" },
                    { new Guid("70f9f0ce-f11f-2bfa-58b9-3703249867cc"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Espectadores a la vez en una transmisión.", "live.max_viewers", null, "50" },
                    { new Guid("eab36100-4af5-9b8d-b00f-4e8203a9417c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos máximos de una transmisión. Al cumplirse se corta.", "live.max_minutes", null, "30" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_status_visibility",
                table: "live_streams",
                columns: new[] { "status", "visibility" });

            migrationBuilder.CreateIndex(
                name: "ux_live_streams_public_path",
                table: "live_streams",
                column: "public_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_viewer_sessions_expires_at",
                table: "live_viewer_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_live_viewer_sessions_live_stream_id",
                table: "live_viewer_sessions",
                column: "live_stream_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_viewer_sessions");

            migrationBuilder.DropIndex(
                name: "ix_live_streams_status_visibility",
                table: "live_streams");

            migrationBuilder.DropIndex(
                name: "ux_live_streams_public_path",
                table: "live_streams");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("2c874b2f-9844-8461-9122-0f4519708016"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("35b463b4-9326-13a2-69ad-b1bf4404391d"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("70f9f0ce-f11f-2bfa-58b9-3703249867cc"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("eab36100-4af5-9b8d-b00f-4e8203a9417c"));

            migrationBuilder.DropColumn(
                name: "hidden_by_staff",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "hidden_reason",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "public_path",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "publish_key_expires_at",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "publish_key_hash",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "publish_key_used_at",
                table: "live_streams");

            migrationBuilder.DropColumn(
                name: "recording_path",
                table: "live_streams");

            migrationBuilder.AlterColumn<Point>(
                name: "location",
                table: "live_streams",
                type: "geography(point, 4326)",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geography(point, 4326)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "category_id",
                table: "live_streams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

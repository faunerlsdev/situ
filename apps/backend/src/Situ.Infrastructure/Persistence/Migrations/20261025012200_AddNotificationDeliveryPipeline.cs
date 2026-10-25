using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_recipient_id",
                table: "notifications");

            migrationBuilder.AddColumn<int>(
                name: "bundle_count",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "group_key",
                table: "notifications",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_nearby_reports",
                table: "notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "notification_settings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Bogota");

            migrationBuilder.CreateTable(
                name: "device_alert_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_alert_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_alert_locations_device_id",
                        column: x => x.device_id,
                        principalTable: "user_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_device_alert_locations_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_jobs", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("1582f5f1-5156-ade8-b3cd-2cbaf2905437"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ciudades de interés adicionales permitidas. Hoy aplica a todos los planes.", "notifications.max_interest_cities", null, "3" },
                    { new Guid("67f468e6-630f-9cca-09d7-9609ba28d80d"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pushes que una persona puede recibir en una hora antes de agruparlos.", "notifications.max_pushes_per_hour", null, "6" },
                    { new Guid("896ff7b6-df57-8b7d-ebbc-4b9899a9b78a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Trabajos de notificación que se procesan en cada lote.", "notifications.dispatch_batch_size", null, "50" },
                    { new Guid("945fc2ee-3ace-90a5-4898-cbebe06ebdd6"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Mensajes máximos por lote hacia Expo Push.", "notifications.push_batch_size", null, "100" },
                    { new Guid("bd6ba2ae-be95-8c7a-614f-e87eb7d9a118"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos que vive la ubicación aproximada usada solo para alertas de proximidad.", "notifications.alert_location_ttl_minutes", null, "30" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notifications_recipient_id_group_key",
                table: "notifications",
                columns: new[] { "recipient_id", "group_key" },
                unique: true,
                filter: "group_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_recipient_id_target_type_target_id",
                table: "notifications",
                columns: new[] { "recipient_id", "target_type", "target_id" },
                unique: true,
                filter: "target_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_device_alert_locations_expires_at",
                table: "device_alert_locations",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_device_alert_locations_location",
                table: "device_alert_locations",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_device_alert_locations_user_id",
                table: "device_alert_locations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_device_alert_locations_device_id",
                table: "device_alert_locations",
                column: "device_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_notification_jobs_status_available_at",
                table: "notification_jobs",
                columns: new[] { "status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "ux_notification_jobs_kind_subject_id",
                table: "notification_jobs",
                columns: new[] { "kind", "subject_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_alert_locations");

            migrationBuilder.DropTable(
                name: "notification_jobs");

            migrationBuilder.DropIndex(
                name: "ix_notifications_recipient_id_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ux_notifications_recipient_id_group_key",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ux_notifications_recipient_id_target_type_target_id",
                table: "notifications");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("1582f5f1-5156-ade8-b3cd-2cbaf2905437"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("67f468e6-630f-9cca-09d7-9609ba28d80d"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("896ff7b6-df57-8b7d-ebbc-4b9899a9b78a"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("945fc2ee-3ace-90a5-4898-cbebe06ebdd6"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("bd6ba2ae-be95-8c7a-614f-e87eb7d9a118"));

            migrationBuilder.DropColumn(
                name: "bundle_count",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "group_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "notify_nearby_reports",
                table: "notification_settings");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "notification_settings");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id",
                table: "notifications",
                column: "recipient_id");
        }
    }
}

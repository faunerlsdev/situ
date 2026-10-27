using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "friend_location_consent_version",
                table: "user_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "friend_location_consented_at",
                table: "user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "friend_location_reminded_at",
                table: "user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_location_consent_version",
                table: "user_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "public_location_consented_at",
                table: "user_profiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "public_location_mode",
                table: "user_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Hidden");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "user_locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_live",
                table: "user_locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_seen_at",
                table: "user_locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "privacy_zones",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Hidden");

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("06eb53a8-94ab-1ec5-acee-5b3e4178a0a0"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión del texto para mostrar ubicación en el mapa general.", "location.public_consent_version", null, "2026-10-27" },
                    { new Guid("128d7838-6ee6-c49e-2acb-fdeff732d8d7"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Precisión, en metros, del punto del mapa general.", "location.public_precision_meters", null, "300" },
                    { new Guid("20173fba-cf21-cdbd-46ca-9620bd2a013c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Antigüedad máxima, en segundos, de una lectura.", "location.max_reading_age_seconds", null, "120" },
                    { new Guid("2e33c43c-469d-bb33-4401-781532709dbc"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Lecturas de ubicación que una persona puede enviar en un minuto.", "location.posts_per_minute", null, "4" },
                    { new Guid("3f813616-1157-1b19-db0b-3f6b38330a3f"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos a los que se redondea la hora de última vez visto.", "location.last_seen_round_minutes", null, "5" },
                    { new Guid("4ee1bcf5-a2cf-11c2-f9af-a13819a96ba6"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto del consentimiento para compartir ubicación con amigos.", "location.friend_consent_text", null, "Si compartes tu ubicación, tus amigos aceptados verán un punto aproximado mientras la app esté abierta. No se envía en segundo plano. Puedes apagarlo, elegir amigos, poner una duración o una zona donde no se comparte. Un menor necesita la autorización de su representante." },
                    { new Guid("88215097-9201-639d-da29-9daebaabcd71"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos que vive una ubicación compartida sin una lectura nueva.", "location.ttl_minutes", null, "15" },
                    { new Guid("909fff72-8081-bb22-08c5-ad615bf3de43"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto del consentimiento para mostrar ubicación en el mapa general.", "location.public_consent_text", null, "Si muestras tu ubicación en el mapa general, cualquier persona con cuenta puede ver un punto aproximado, mucho menos preciso que el de tus amigos. Puedes ocultarla cuando quieras. Un menor no aparece en el mapa general." },
                    { new Guid("b6b7bbd4-bff5-e1a5-140a-46537a6662d9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Días entre recordatorios de que un menor comparte ubicación con amigos.", "location.minor_reminder_days", null, "7" },
                    { new Guid("cc22dbcf-c640-cf29-9e9e-e052a3019eda"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Segundos entre lecturas mientras la app está en primer plano.", "location.emit_interval_seconds", null, "20" },
                    { new Guid("d82023c8-a0d5-c240-13e7-9d8f665b2d6a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión del texto para compartir ubicación con amigos.", "location.friend_consent_version", null, "2026-10-27" },
                    { new Guid("e2fe6175-a0c7-0905-07d6-c5fc1798af43"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Precisión, en metros, del punto que ven los amigos.", "location.friend_precision_meters", null, "25" },
                    { new Guid("eef6fec6-9551-671c-80be-ede41e354976"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Precisión máxima, en metros, que se acepta de una lectura.", "location.max_accuracy_meters", null, "200" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("06eb53a8-94ab-1ec5-acee-5b3e4178a0a0"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("128d7838-6ee6-c49e-2acb-fdeff732d8d7"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("20173fba-cf21-cdbd-46ca-9620bd2a013c"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("2e33c43c-469d-bb33-4401-781532709dbc"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("3f813616-1157-1b19-db0b-3f6b38330a3f"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("4ee1bcf5-a2cf-11c2-f9af-a13819a96ba6"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("88215097-9201-639d-da29-9daebaabcd71"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("909fff72-8081-bb22-08c5-ad615bf3de43"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("b6b7bbd4-bff5-e1a5-140a-46537a6662d9"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("cc22dbcf-c640-cf29-9e9e-e052a3019eda"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("d82023c8-a0d5-c240-13e7-9d8f665b2d6a"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("e2fe6175-a0c7-0905-07d6-c5fc1798af43"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("eef6fec6-9551-671c-80be-ede41e354976"));

            migrationBuilder.DropColumn(
                name: "friend_location_consent_version",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "friend_location_consented_at",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "friend_location_reminded_at",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "public_location_consent_version",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "public_location_consented_at",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "public_location_mode",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "user_locations");

            migrationBuilder.DropColumn(
                name: "is_live",
                table: "user_locations");

            migrationBuilder.DropColumn(
                name: "last_seen_at",
                table: "user_locations");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "privacy_zones");
        }
    }
}

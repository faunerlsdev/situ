using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfileVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_profile_consent_version",
                table: "user_profiles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_bio",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_public_reports",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("23a30842-e839-4e23-259d-b779e99e7618"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión del texto que hay que aceptar para hacer público el perfil.", "profile.public_consent_version", null, "2026-10-27" },
                    { new Guid("3a59a26f-4995-8c54-60e8-bb7fdb2c47a6"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Búsquedas de usuario que una persona puede hacer en un minuto.", "friends.search_per_minute", null, "10" },
                    { new Guid("9d2644d0-57c2-debd-eb6e-acbfa5c45264"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solicitudes de amistad pendientes que una persona puede tener abiertas.", "friends.max_pending", null, "30" },
                    { new Guid("9f7d1f8e-8d78-2284-74b4-a136c9b40f31"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Invitaciones por correo que una persona puede enviar en 24 horas.", "friends.max_invitations_per_day", null, "10" },
                    { new Guid("a67e6ba4-67e3-2861-42d8-e911dae879ba"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solicitudes de amistad que una persona puede enviar en 24 horas.", "friends.max_requests_per_day", null, "20" },
                    { new Guid("e522a2eb-6914-7c38-b881-be47b8e8741d"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Texto del consentimiento para hacer público el perfil.", "profile.public_consent_text", null, "Si haces público tu perfil, cualquier persona con cuenta puede ver tu nombre visible, tu usuario, tu foto, tu biografía y los reportes que publicas con tu nombre. Tu ubicación en el mapa puede mostrarse si dejas activa la ubicación en tiempo real. Puedes volver a privado cuando quieras. Un menor de edad no puede tener perfil público." },
                    { new Guid("e54152a3-1bc2-8e35-f396-1586d7ccd73c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Si es false, un adulto y un menor no pueden ser amigos ni enviarse solicitudes.", "friends.allow_adult_minor", null, "false" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("23a30842-e839-4e23-259d-b779e99e7618"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("3a59a26f-4995-8c54-60e8-bb7fdb2c47a6"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("9d2644d0-57c2-debd-eb6e-acbfa5c45264"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("9f7d1f8e-8d78-2284-74b4-a136c9b40f31"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("a67e6ba4-67e3-2861-42d8-e911dae879ba"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("e522a2eb-6914-7c38-b881-be47b8e8741d"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("e54152a3-1bc2-8e35-f396-1586d7ccd73c"));

            migrationBuilder.DropColumn(
                name: "public_profile_consent_version",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "show_bio",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "show_public_reports",
                table: "user_profiles");
        }
    }
}

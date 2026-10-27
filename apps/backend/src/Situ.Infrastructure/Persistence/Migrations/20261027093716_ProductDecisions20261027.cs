using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductDecisions20261027 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users SET phone = NULL WHERE phone = '3000000000';
                UPDATE users
                SET phone = '+57' || regexp_replace(phone, '[^0-9]', '', 'g')
                WHERE phone IS NOT NULL AND phone NOT LIKE '+%';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<bool>(
                name: "remember_web_session",
                table: "user_devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("020d75f9-3000-6b8e-e036-959bcde9c2ae"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Si es true, un menor debe dejar teléfono al registrarse. Pregunta abierta de minimización (RF-MIN-06).", "registration.phone_required_for_minors", null, "true" },
                    { new Guid("440b9908-7298-e9b6-65cf-08d6870e3eef"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Muestra las pestañas Mensajes y Chisme. Apagado las oculta sin borrar las pantallas.", "features.social_tabs", null, "false" },
                    { new Guid("af927380-2343-0c1c-da91-295b13b6646f"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Días de la sesión web. Cada renovación vuelve a contar este plazo.", "web.session_refresh_days", null, "30" },
                    { new Guid("edc5c736-fadd-3768-ad85-b11d191d7cac"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Días de la sesión web cuando la persona marca recordarme en este dispositivo.", "web.session_remember_days", null, "90" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("020d75f9-3000-6b8e-e036-959bcde9c2ae"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("440b9908-7298-e9b6-65cf-08d6870e3eef"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("af927380-2343-0c1c-da91-295b13b6646f"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("edc5c736-fadd-3768-ad85-b11d191d7cac"));

            migrationBuilder.DropColumn(
                name: "remember_web_session",
                table: "user_devices");

            migrationBuilder.Sql("UPDATE users SET phone = '3000000000' WHERE phone IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianConsentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_attempts",
                table: "security_tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "authorization_channel",
                table: "guardian_consents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decline_reason",
                table: "guardian_consents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "minor_statement",
                table: "guardian_consents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "guardian_consent_dispatchs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardian_consent_dispatchs", x => x.id);
                    table.ForeignKey(
                        name: "fk_guardian_consent_dispatchs_minor_user_id",
                        column: x => x.minor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guardian_consent_expiry_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    personal_data_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardian_consent_expiry_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guardian_email_opt_outs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardian_email_opt_outs", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("0198133f-f2c7-48ff-b35f-9204170ead5f"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Reenvíos del código del representante cada 15 minutos.", "guardian.max_code_resends_per_15_minutes", null, "3" },
                    { new Guid("59f3b24c-ced4-fdc8-9aad-a76c14a885eb"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solicitudes de autorización que una misma IP puede iniciar en una hora.", "guardian.max_sends_per_ip_per_hour", null, "10" },
                    { new Guid("6be3ab7e-c5a4-8535-09be-2711ea1ec4a2"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos mínimos entre correos de autorización de un mismo menor.", "guardian.min_minutes_between_sends", null, "2" },
                    { new Guid("7b7b2b17-e14e-6479-c4fc-90345df0ddc5"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cambios del correo del representante que un menor puede hacer en 24 horas.", "guardian.max_email_changes_per_day", null, "3" },
                    { new Guid("91b88a67-a2b6-ea6a-196d-08deae416ba7"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Intentos fallidos antes de invalidar el código del representante.", "guardian.max_code_attempts", null, "5" },
                    { new Guid("a0d03d7e-316d-76c1-52b7-9866c4c52fbf"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solicitudes que un mismo correo de representante puede recibir en una hora.", "guardian.max_sends_per_email_per_hour", null, "3" },
                    { new Guid("ba8ded5c-3fce-64d4-82e1-a1370a7e9170"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Correos de autorización que un menor puede enviar en 24 horas.", "guardian.max_sends_per_minor_per_day", null, "5" },
                    { new Guid("e12d5519-958a-1c80-62f9-22ebd854044c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Solicitudes que un mismo correo de representante puede recibir en un día.", "guardian.max_sends_per_email_per_day", null, "10" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consent_dispatchs_destination_email",
                table: "guardian_consent_dispatchs",
                column: "destination_email");

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consent_dispatchs_minor_user_id",
                table: "guardian_consent_dispatchs",
                column: "minor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consent_dispatchs_sent_at",
                table: "guardian_consent_dispatchs",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ux_guardian_consent_expiry_audits_consent_id",
                table: "guardian_consent_expiry_audits",
                column: "consent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_guardian_email_opt_outs_email",
                table: "guardian_email_opt_outs",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardian_consent_dispatchs");

            migrationBuilder.DropTable(
                name: "guardian_consent_expiry_audits");

            migrationBuilder.DropTable(
                name: "guardian_email_opt_outs");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("0198133f-f2c7-48ff-b35f-9204170ead5f"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("59f3b24c-ced4-fdc8-9aad-a76c14a885eb"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("6be3ab7e-c5a4-8535-09be-2711ea1ec4a2"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("7b7b2b17-e14e-6479-c4fc-90345df0ddc5"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("91b88a67-a2b6-ea6a-196d-08deae416ba7"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("a0d03d7e-316d-76c1-52b7-9866c4c52fbf"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("ba8ded5c-3fce-64d4-82e1-a1370a7e9170"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("e12d5519-958a-1c80-62f9-22ebd854044c"));

            migrationBuilder.DropColumn(
                name: "failed_attempts",
                table: "security_tokens");

            migrationBuilder.DropColumn(
                name: "authorization_channel",
                table: "guardian_consents");

            migrationBuilder.DropColumn(
                name: "decline_reason",
                table: "guardian_consents");

            migrationBuilder.DropColumn(
                name: "minor_statement",
                table: "guardian_consents");
        }
    }
}

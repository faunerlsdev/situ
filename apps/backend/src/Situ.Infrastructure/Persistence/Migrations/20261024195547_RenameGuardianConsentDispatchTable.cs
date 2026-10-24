using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameGuardianConsentDispatchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_guardian_consent_dispatchs_minor_user_id",
                table: "guardian_consent_dispatchs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_guardian_consent_dispatchs",
                table: "guardian_consent_dispatchs");

            migrationBuilder.RenameTable(
                name: "guardian_consent_dispatchs",
                newName: "guardian_consent_dispatches");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatchs_sent_at",
                table: "guardian_consent_dispatches",
                newName: "ix_guardian_consent_dispatches_sent_at");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatchs_minor_user_id",
                table: "guardian_consent_dispatches",
                newName: "ix_guardian_consent_dispatches_minor_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatchs_destination_email",
                table: "guardian_consent_dispatches",
                newName: "ix_guardian_consent_dispatches_destination_email");

            migrationBuilder.AddPrimaryKey(
                name: "pk_guardian_consent_dispatches",
                table: "guardian_consent_dispatches",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_guardian_consent_dispatches_minor_user_id",
                table: "guardian_consent_dispatches",
                column: "minor_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_guardian_consent_dispatches_minor_user_id",
                table: "guardian_consent_dispatches");

            migrationBuilder.DropPrimaryKey(
                name: "pk_guardian_consent_dispatches",
                table: "guardian_consent_dispatches");

            migrationBuilder.RenameTable(
                name: "guardian_consent_dispatches",
                newName: "guardian_consent_dispatchs");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatches_sent_at",
                table: "guardian_consent_dispatchs",
                newName: "ix_guardian_consent_dispatchs_sent_at");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatches_minor_user_id",
                table: "guardian_consent_dispatchs",
                newName: "ix_guardian_consent_dispatchs_minor_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_guardian_consent_dispatches_destination_email",
                table: "guardian_consent_dispatchs",
                newName: "ix_guardian_consent_dispatchs_destination_email");

            migrationBuilder.AddPrimaryKey(
                name: "pk_guardian_consent_dispatchs",
                table: "guardian_consent_dispatchs",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_guardian_consent_dispatchs_minor_user_id",
                table: "guardian_consent_dispatchs",
                column: "minor_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

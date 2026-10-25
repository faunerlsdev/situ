using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBlockScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_blocks_blocked_user_id",
                table: "user_blocks");

            migrationBuilder.DropIndex(
                name: "ux_user_blocks_blocker_id_blocked_user_id",
                table: "user_blocks");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "origin_at",
                table: "user_blocks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "origin_target_id",
                table: "user_blocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_target_type",
                table: "user_blocks",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "user_blocks",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Full");

            // Rows from before this column have no origin, so a block-author row cannot be
            // told apart from an explicit block. They stay Full. New block-author calls write AnonymousContentOnly.
            migrationBuilder.Sql("UPDATE user_blocks SET scope = 'Full' WHERE scope = '' OR scope IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "scope",
                table: "user_blocks",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldDefaultValue: "Full");

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocked_user_id_scope",
                table: "user_blocks",
                columns: new[] { "blocked_user_id", "scope" });

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocker_id_scope",
                table: "user_blocks",
                columns: new[] { "blocker_id", "scope" });

            migrationBuilder.CreateIndex(
                name: "ux_user_blocks_blocker_id_blocked_user_id_scope",
                table: "user_blocks",
                columns: new[] { "blocker_id", "blocked_user_id", "scope" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_blocks_blocked_user_id_scope",
                table: "user_blocks");

            migrationBuilder.DropIndex(
                name: "ix_user_blocks_blocker_id_scope",
                table: "user_blocks");

            migrationBuilder.DropIndex(
                name: "ux_user_blocks_blocker_id_blocked_user_id_scope",
                table: "user_blocks");

            migrationBuilder.DropColumn(
                name: "origin_at",
                table: "user_blocks");

            migrationBuilder.DropColumn(
                name: "origin_target_id",
                table: "user_blocks");

            migrationBuilder.DropColumn(
                name: "origin_target_type",
                table: "user_blocks");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "user_blocks");

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocked_user_id",
                table: "user_blocks",
                column: "blocked_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_blocks_blocker_id_blocked_user_id",
                table: "user_blocks",
                columns: new[] { "blocker_id", "blocked_user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}

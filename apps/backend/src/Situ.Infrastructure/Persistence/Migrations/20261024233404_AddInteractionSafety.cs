using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractionSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_complaints_reporter_id",
                table: "complaints");

            migrationBuilder.DropIndex(
                name: "ix_comments_parent_comment_id",
                table: "comments");

            migrationBuilder.CreateTable(
                name: "anonymous_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anonymous_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_anonymous_participants_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("14185d9e-c96d-3229-7427-7c3094a27b79"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Denuncias distintas que mandan un reporte o comentario a revisión.", "moderation.complaint_threshold", null, "3" },
                    { new Guid("3d53c47b-fef6-42da-0d4d-588119b2c52a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Días de gracia antes de borrar una cuenta.", "account.deletion_grace_days", null, "14" },
                    { new Guid("3ff56be3-0b28-1e3c-a215-8b976675fed3"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Comentarios anónimos que una persona puede publicar por día.", "comments.max_anonymous_per_day", null, "30" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_complaints_status_is_priority_created_at",
                table: "complaints",
                columns: new[] { "status", "is_priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_complaints_target_type_target_id_status",
                table: "complaints",
                columns: new[] { "target_type", "target_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_complaints_reporter_id_target_type_target_id",
                table: "complaints",
                columns: new[] { "reporter_id", "target_type", "target_id" },
                unique: true,
                filter: "status IN ('Open', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_comment_id_created_at",
                table: "comments",
                columns: new[] { "parent_comment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_target_type_target_id_created_at",
                table: "comments",
                columns: new[] { "target_type", "target_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_requests_status_scheduled_for",
                table: "account_deletion_requests",
                columns: new[] { "status", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "ix_anonymous_participants_user_id",
                table: "anonymous_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_anonymous_participants_target_type_target_id_index",
                table: "anonymous_participants",
                columns: new[] { "target_type", "target_id", "index" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_anonymous_participants_target_type_target_id_user_id",
                table: "anonymous_participants",
                columns: new[] { "target_type", "target_id", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anonymous_participants");

            migrationBuilder.DropIndex(
                name: "ix_complaints_status_is_priority_created_at",
                table: "complaints");

            migrationBuilder.DropIndex(
                name: "ix_complaints_target_type_target_id_status",
                table: "complaints");

            migrationBuilder.DropIndex(
                name: "ux_complaints_reporter_id_target_type_target_id",
                table: "complaints");

            migrationBuilder.DropIndex(
                name: "ix_comments_parent_comment_id_created_at",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "ix_comments_target_type_target_id_created_at",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "ix_account_deletion_requests_status_scheduled_for",
                table: "account_deletion_requests");

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("14185d9e-c96d-3229-7427-7c3094a27b79"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("3d53c47b-fef6-42da-0d4d-588119b2c52a"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("3ff56be3-0b28-1e3c-a215-8b976675fed3"));

            migrationBuilder.CreateIndex(
                name: "ix_complaints_reporter_id",
                table: "complaints",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_comment_id",
                table: "comments",
                column: "parent_comment_id");
        }
    }
}

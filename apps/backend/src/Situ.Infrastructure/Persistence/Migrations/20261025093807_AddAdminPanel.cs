using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_staff_assignments_user_id",
                table: "staff_assignments");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs");

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("1dd1ecff-11fd-9fb0-a895-12b014fd631b"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("2e7ee9eb-dbb6-57e6-7bd8-09a0a8774ae1"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("99024685-53d0-5a97-a1b0-5449db059959"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("ae2eb5e3-c40f-bf8e-af63-c5ae9d127107"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("b3851540-18bd-1157-06ff-d21ae57bcce7"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("f19d0beb-ec3e-a8c0-4104-648e2649f829"));

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "id", "code", "created_at", "role_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("23da6b39-685e-a211-8bf9-6c0c9e50dbb5"), "avatars.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), null },
                    { new Guid("4a8b4e56-510a-9e7b-2e0c-e5d4afe77f74"), "plans.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), null },
                    { new Guid("a5873112-5b19-60dc-fac4-e2bc9f96eb38"), "legalholds.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), null },
                    { new Guid("ef088217-861f-2045-bbea-8e5421697644"), "metrics.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), null },
                    { new Guid("f655739b-071e-44b3-e2ad-17ee6db01580"), "users.view_email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), null }
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e"),
                column: "description",
                value: "Consulta cuentas.");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"),
                column: "description",
                value: "Resuelve denuncias, suspende cuentas, corta vivos y atiende retiros.");

            migrationBuilder.InsertData(
                table: "staff_roles",
                columns: new[] { "id", "created_at", "description", "is_system", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("7ac4f0db-56d1-57dc-8c56-9c53ac0754e7"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Consulta la bitácora.", true, "Auditor", null },
                    { new Guid("fc1139b4-81a8-cf3e-eb30-e04a23ed73aa"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administra categorías y consulta los planes.", true, "Gestor de contenido", null }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[] { new Guid("68b26cde-9190-6c55-6379-30551fa0de80"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Textos del panel inferior por país e idioma, en JSON.", "panel.copy_by_country", null, "[]" });

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "id", "code", "created_at", "role_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("1b8e09d7-6159-d681-8159-ca76470fa747"), "audit.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("7ac4f0db-56d1-57dc-8c56-9c53ac0754e7"), null },
                    { new Guid("883bd998-1005-2bce-16ee-380766ac0123"), "plans.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("fc1139b4-81a8-cf3e-eb30-e04a23ed73aa"), null },
                    { new Guid("c1bfe17e-42e4-e85f-1be2-b93f1ff4e11a"), "categories.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("fc1139b4-81a8-cf3e-eb30-e04a23ed73aa"), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_takedown_requests_requester_email",
                table: "takedown_requests",
                column: "requester_email");

            migrationBuilder.CreateIndex(
                name: "ix_takedown_requests_status_due_at",
                table: "takedown_requests",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_assignments_user_id_is_active",
                table: "staff_assignments",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_entity_type",
                table: "audit_logs",
                columns: new[] { "action", "entity_type" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_takedown_requests_requester_email",
                table: "takedown_requests");

            migrationBuilder.DropIndex(
                name: "ix_takedown_requests_status_due_at",
                table: "takedown_requests");

            migrationBuilder.DropIndex(
                name: "ix_staff_assignments_user_id_is_active",
                table: "staff_assignments");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_action_entity_type",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_actor_user_id_occurred_at",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_occurred_at",
                table: "audit_logs");

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("1b8e09d7-6159-d681-8159-ca76470fa747"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("23da6b39-685e-a211-8bf9-6c0c9e50dbb5"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("4a8b4e56-510a-9e7b-2e0c-e5d4afe77f74"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("883bd998-1005-2bce-16ee-380766ac0123"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("a5873112-5b19-60dc-fac4-e2bc9f96eb38"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("c1bfe17e-42e4-e85f-1be2-b93f1ff4e11a"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("ef088217-861f-2045-bbea-8e5421697644"));

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "id",
                keyValue: new Guid("f655739b-071e-44b3-e2ad-17ee6db01580"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("68b26cde-9190-6c55-6379-30551fa0de80"));

            migrationBuilder.DeleteData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("7ac4f0db-56d1-57dc-8c56-9c53ac0754e7"));

            migrationBuilder.DeleteData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("fc1139b4-81a8-cf3e-eb30-e04a23ed73aa"));

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "id", "code", "created_at", "role_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("1dd1ecff-11fd-9fb0-a895-12b014fd631b"), "categories.manage", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"), null },
                    { new Guid("2e7ee9eb-dbb6-57e6-7bd8-09a0a8774ae1"), "audit.view", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"), null },
                    { new Guid("99024685-53d0-5a97-a1b0-5449db059959"), "takedowns.handle", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e"), null },
                    { new Guid("ae2eb5e3-c40f-bf8e-af63-c5ae9d127107"), "anonymity.reveal", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"), null },
                    { new Guid("b3851540-18bd-1157-06ff-d21ae57bcce7"), "organizations.verify", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"), null },
                    { new Guid("f19d0beb-ec3e-a8c0-4104-648e2649f829"), "complaints.resolve", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e"), null }
                });

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e"),
                column: "description",
                value: "Atiende cuentas, quejas y solicitudes de retiro.");

            migrationBuilder.UpdateData(
                table: "staff_roles",
                keyColumn: "id",
                keyValue: new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"),
                column: "description",
                value: "Modera contenido, cortes de vivos, quejas y anonimato.");

            migrationBuilder.CreateIndex(
                name: "ix_staff_assignments_user_id",
                table: "staff_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");
        }
    }
}

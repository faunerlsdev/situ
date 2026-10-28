using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifyNearbyLives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notify_nearby_lives",
                table: "notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[] { new Guid("ea3d6bb8-8f1a-a703-afa5-171a4040b124"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Metros alrededor de una transmisión pública para avisar a quien tenga una ubicación de alertas vigente. No usa la ubicación en segundo plano.", "live.notify_radius_m", null, "1000" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("ea3d6bb8-8f1a-a703-afa5-171a4040b124"));

            migrationBuilder.DropColumn(
                name: "notify_nearby_lives",
                table: "notification_settings");
        }
    }
}

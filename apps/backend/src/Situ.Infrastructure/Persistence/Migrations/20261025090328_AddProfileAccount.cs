using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "user_name_changed_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE user_profiles SET bio = left(bio, 160) WHERE bio IS NOT NULL AND char_length(bio) > 160;");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_users_user_name_lower ON users (lower(user_name));");

            migrationBuilder.AlterColumn<string>(
                name: "bio",
                table: "user_profiles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(280)",
                oldMaxLength: 280,
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "avatar_gallery_items",
                columns: new[] { "id", "created_at", "image_url", "is_active", "name", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { new Guid("105bee35-295a-db26-02b9-c83e01167fc8"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/07.png", true, "Noticias", 7, null },
                    { new Guid("14f682a3-9c8a-ab87-7ab2-23194369c67c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/08.png", true, "Buscar", 8, null },
                    { new Guid("17c39a1a-0fb8-c9d2-8289-59aa4c9a721f"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/03.png", true, "Megáfono", 3, null },
                    { new Guid("58a6631b-5ea8-8009-11bd-c6f9232820fa"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/02.png", true, "Pin", 2, null },
                    { new Guid("721a65bc-05fb-172f-d98b-0d7a64dc88fa"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/11.png", true, "Evento", 11, null },
                    { new Guid("72628ba1-f367-e6bb-1a7b-77eceefa2d6e"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/12.png", true, "Vía", 12, null },
                    { new Guid("7cbf7190-f4fa-eedf-07a0-8ab678bb1704"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/06.png", true, "Mensajes", 6, null },
                    { new Guid("8cf97b9e-c6cb-ebbd-d2c5-0e834f1c16a9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/05.png", true, "En vivo", 5, null },
                    { new Guid("938ee6eb-b5d6-ebb0-2b66-c6b064a6c01a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/09.png", true, "Isotipo oscuro", 9, null },
                    { new Guid("d3965a1e-3fc6-9ac3-b75e-03569a1b1d1b"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/04.png", true, "Mapa", 4, null },
                    { new Guid("e0815563-ad7d-2dd9-c6f1-c17b4da44701"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/01.png", true, "Isotipo", 1, null },
                    { new Guid("e596b987-b91b-727a-d122-2508e68a4dd5"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "/assets/avatars/10.png", true, "Genérico", 10, null }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("1fcb5b43-144a-32c5-5926-77304b18e23a"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Días mínimos entre cambios del nombre de usuario.", "profile.username_change_days", null, "30" },
                    { new Guid("40274796-e1be-33dd-c421-d47bcc59127f"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "URL de la política de tratamiento de datos.", "legal.privacy_url", null, "https://somossitu.com/privacidad" },
                    { new Guid("4907ff6e-2b6b-e914-9bd2-b22d60e80acd"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Términos que no pueden aparecer en el nombre, el usuario o la bio.", "profile.blocked_terms", null, "odio,nazi,porn,xxx,puta,mierda" },
                    { new Guid("54937a00-04dc-ef31-4bf8-63a7ca435f32"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "URL de soporte.", "legal.support_url", null, "https://somossitu.com/soporte" },
                    { new Guid("b6e4d61b-f8cc-b20a-c148-d69513e1bfcc"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tamaño máximo de una foto de perfil, en bytes.", "profile.avatar_max_bytes", null, "5242880" },
                    { new Guid("c84209b0-abbf-981d-f5c7-5e373133afd9"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Nombres de usuario reservados, separados por coma. Se tratan igual que un nombre ya tomado.", "profile.reserved_usernames", null, "admin,administrador,soporte,support,situ,somossitu,moderador,moderator,ayuda,help,root,system,sistema,staff,oficial" },
                    { new Guid("d01def08-e123-709a-9103-7d69b8e4d0b6"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "URL de los términos de uso.", "legal.terms_url", null, "https://somossitu.com/terminos" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("105bee35-295a-db26-02b9-c83e01167fc8"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("14f682a3-9c8a-ab87-7ab2-23194369c67c"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("17c39a1a-0fb8-c9d2-8289-59aa4c9a721f"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("58a6631b-5ea8-8009-11bd-c6f9232820fa"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("721a65bc-05fb-172f-d98b-0d7a64dc88fa"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("72628ba1-f367-e6bb-1a7b-77eceefa2d6e"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("7cbf7190-f4fa-eedf-07a0-8ab678bb1704"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("8cf97b9e-c6cb-ebbd-d2c5-0e834f1c16a9"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("938ee6eb-b5d6-ebb0-2b66-c6b064a6c01a"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("d3965a1e-3fc6-9ac3-b75e-03569a1b1d1b"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("e0815563-ad7d-2dd9-c6f1-c17b4da44701"));

            migrationBuilder.DeleteData(
                table: "avatar_gallery_items",
                keyColumn: "id",
                keyValue: new Guid("e596b987-b91b-727a-d122-2508e68a4dd5"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("1fcb5b43-144a-32c5-5926-77304b18e23a"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("40274796-e1be-33dd-c421-d47bcc59127f"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("4907ff6e-2b6b-e914-9bd2-b22d60e80acd"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("54937a00-04dc-ef31-4bf8-63a7ca435f32"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("b6e4d61b-f8cc-b20a-c148-d69513e1bfcc"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("c84209b0-abbf-981d-f5c7-5e373133afd9"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("d01def08-e123-709a-9103-7d69b8e4d0b6"));

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_users_user_name_lower;");

            migrationBuilder.DropColumn(
                name: "user_name_changed_at",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "bio",
                table: "user_profiles",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);
        }
    }
}

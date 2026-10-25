using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportVisibilityAndCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_author_id",
                table: "reports");

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("4c9c8d83-0acf-4a06-a01a-741463642574"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("64d82747-b8d5-a72e-d1e5-70190a90ea12"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("6f916ffa-7888-abc4-94b5-3369f7955b9a"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("719d1e3b-7764-63f5-eb4e-3391cfedd76e"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("7ff00faf-5a30-f419-d5f3-25240c9be956"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("896328c8-018a-02bf-a488-c3b2906cf314"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("9e19ae16-481b-cc89-b257-dc51d75c6c1e"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("a3656a6c-4570-8435-65d3-4c8e44c5adf0"));

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "reports",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Public");

            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "report_media",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "report_media",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.InsertData(
                table: "report_categories",
                columns: new[] { "id", "button_icon_url", "code", "color_hex", "created_at", "description", "importance_weight", "is_active", "is_adult_only", "map_icon_url", "media_requirement", "name", "parent_category_id", "sort_order", "ttl_minutes", "updated_at" },
                values: new object[,]
                {
                    { new Guid("28671b9d-62e5-cdb7-3d9c-c825a4e523d6"), "/assets/categories/inundacion.png", "inundacion", "#039BE5", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Calles anegadas, crecientes y desbordamientos.", 8.0, true, false, "/assets/categories/inundacion.png", "None", "Inundación", null, 7, 360, null },
                    { new Guid("3861576d-40b5-9671-fce7-260f1fc63e98"), "/assets/categories/basuras.png", "basuras", "#43A047", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Basura acumulada, escombros o vertimientos.", 2.0, true, false, "/assets/categories/basuras.png", "None", "Basuras", null, 4, 1440, null },
                    { new Guid("546ba84a-00b0-6347-276f-36084cec1fa5"), "/assets/categories/derrumbe.png", "derrumbe", "#6D4C41", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Derrumbes y material sobre la vía.", 8.0, true, false, "/assets/categories/derrumbe.png", "None", "Derrumbe", null, 6, 480, null },
                    { new Guid("55313b4b-bfcb-9343-4b29-2d7cd06cea72"), "/assets/categories/accidente_transito.png", "accidente_transito", "#E53935", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Choques, atropellos y vehículos varados.", 8.0, true, false, "/assets/categories/accidente_transito.png", "None", "Accidente de tránsito", null, 1, 90, null },
                    { new Guid("5ac7e05f-5847-7558-956c-063441e1db3b"), "/assets/categories/via_cerrada.png", "via_cerrada", "#FB8C00", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cierres y tramos que no se pueden pasar.", 6.0, true, false, "/assets/categories/via_cerrada.png", "None", "Vía cerrada", null, 5, 240, null },
                    { new Guid("839bd530-255b-36d0-fd39-c9334172d621"), "/assets/categories/incendio.png", "incendio", "#F4511E", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Incendios, humo o explosiones.", 9.0, true, false, "/assets/categories/incendio.png", "None", "Incendio", null, 9, 180, null },
                    { new Guid("83cb3751-e08a-7f93-d36a-518e809adfbd"), "/assets/categories/puesto_control.png", "puesto_control", "#1E88E5", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Retenes, controles y puestos de vigilancia.", 4.0, true, false, "/assets/categories/puesto_control.png", "None", "Puesto de control", null, 3, 120, null },
                    { new Guid("84757588-1679-13d9-8580-e6b189f51307"), "/assets/categories/manifestacion.png", "manifestacion", "#8D6E63", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Marchas, plantones y bloqueos.", 6.0, true, false, "/assets/categories/manifestacion.png", "None", "Manifestación", null, 8, 180, null },
                    { new Guid("8e0cf07e-5a2a-04b8-eb81-107d47d4b7ad"), "/assets/categories/hurto.png", "hurto", "#8E24AA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Hurtos que están ocurriendo ahora.", 7.0, true, false, "/assets/categories/hurto.png", "None", "Hurto", null, 2, 240, null },
                    { new Guid("c9238d94-25ec-683c-26b0-7246af41e7d5"), "/assets/categories/evento.png", "evento", "#7CB342", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eventos que ocupan el espacio público.", 2.0, true, false, "/assets/categories/evento.png", "None", "Evento", null, 12, 240, null },
                    { new Guid("d4b90458-fde7-f8b2-9696-130c76805d82"), "/assets/categories/corte_servicio.png", "corte_servicio", "#546E7A", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cortes de agua, luz o gas.", 4.0, true, false, "/assets/categories/corte_servicio.png", "None", "Corte de servicio", null, 11, 360, null },
                    { new Guid("e9c62b0b-a002-63b6-5d01-084e5ea731ac"), "/assets/categories/obra_vial.png", "obra_vial", "#FDD835", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Obras que cambian el paso por la vía.", 3.0, true, false, "/assets/categories/obra_vial.png", "None", "Obra vial", null, 10, 720, null }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "created_at", "description", "key", "updated_at", "value" },
                values: new object[,]
                {
                    { new Guid("299ad7d3-1684-a379-8afe-c0eaec3c5672"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tamaño máximo de una imagen de reporte, en bytes.", "reports.max_image_bytes", null, "10485760" },
                    { new Guid("36ce246e-89eb-34b6-596d-491bfe5204c5"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Versión del catálogo de categorías. Sube en cada cambio del panel.", "reports.catalog_version", null, "1" },
                    { new Guid("49d155ed-dc35-1873-210d-f1b359285f50"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Distancia máxima, en metros, para confirmar un reporte.", "reports.confirmation_radius_meters", null, "2000" },
                    { new Guid("54b1448c-e468-4228-f918-253bbef3988b"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Imágenes máximas por reporte.", "reports.max_images", null, "5" },
                    { new Guid("56be27e0-278c-1ad7-4bde-746657f1b749"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Duración máxima de un video de reporte, en segundos.", "reports.max_video_seconds", null, "60" },
                    { new Guid("838da7e2-a185-60c6-dbd3-0867c5bda4fd"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Videos máximos por reporte.", "reports.max_videos", null, "1" },
                    { new Guid("b2940ee3-8bc6-0e79-a434-f503cfbe747c"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tamaño máximo de un video de reporte, en bytes.", "reports.max_video_bytes", null, "104857600" },
                    { new Guid("b9f7f0f4-15ff-1dde-0f03-62118010e53b"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Minutos mínimos que suma un voto de sigue ahí.", "reports.confirmation_min_extension_minutes", null, "10" },
                    { new Guid("d1fe7f63-d6e0-f77c-5cc3-94c70071f355"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Horas antes de borrar un archivo que no se asoció a un reporte.", "reports.pending_media_hours", null, "24" },
                    { new Guid("fbb88722-e86e-e028-2f51-533f20ee0dfb"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Porcentaje del TTL que suma un voto de sigue ahí.", "reports.confirmation_extension_percent", null, "25" }
                });

            migrationBuilder.InsertData(
                table: "report_category_translations",
                columns: new[] { "id", "category_id", "created_at", "description", "locale", "name", "updated_at" },
                values: new object[,]
                {
                    { new Guid("1ebcc22d-ad6c-86b7-e963-8d2087157fc1"), new Guid("55313b4b-bfcb-9343-4b29-2d7cd06cea72"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Choques, atropellos y vehículos varados.", "es-CO", "Accidente de tránsito", null },
                    { new Guid("2f92942a-bbe0-ecfc-a451-8115678d164c"), new Guid("c9238d94-25ec-683c-26b0-7246af41e7d5"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Eventos que ocupan el espacio público.", "es-CO", "Evento", null },
                    { new Guid("42925d6a-4c74-2870-7698-9acc3a09d232"), new Guid("e9c62b0b-a002-63b6-5d01-084e5ea731ac"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Obras que cambian el paso por la vía.", "es-CO", "Obra vial", null },
                    { new Guid("440d0708-b772-7936-a382-1b32ab8fc93c"), new Guid("d4b90458-fde7-f8b2-9696-130c76805d82"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cortes de agua, luz o gas.", "es-CO", "Corte de servicio", null },
                    { new Guid("47646830-7260-864a-8747-4c455afd27fb"), new Guid("84757588-1679-13d9-8580-e6b189f51307"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Marchas, plantones y bloqueos.", "es-CO", "Manifestación", null },
                    { new Guid("586f102d-b247-fb01-652c-ea2e1cae24a4"), new Guid("546ba84a-00b0-6347-276f-36084cec1fa5"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Derrumbes y material sobre la vía.", "es-CO", "Derrumbe", null },
                    { new Guid("67ae33a0-b736-406c-7b56-30a6ca5561a9"), new Guid("8e0cf07e-5a2a-04b8-eb81-107d47d4b7ad"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Hurtos que están ocurriendo ahora.", "es-CO", "Hurto", null },
                    { new Guid("96e621c4-6abd-c66d-b7e7-d3e8aea34ea7"), new Guid("5ac7e05f-5847-7558-956c-063441e1db3b"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cierres y tramos que no se pueden pasar.", "es-CO", "Vía cerrada", null },
                    { new Guid("983f528d-a6b7-6e77-6982-04b7d3aa210d"), new Guid("83cb3751-e08a-7f93-d36a-518e809adfbd"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Retenes, controles y puestos de vigilancia.", "es-CO", "Puesto de control", null },
                    { new Guid("9b0307aa-aefa-eca4-d2f7-9f773dd6b375"), new Guid("3861576d-40b5-9671-fce7-260f1fc63e98"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Basura acumulada, escombros o vertimientos.", "es-CO", "Basuras", null },
                    { new Guid("d0f5a633-6dae-a29a-76c4-9358294d37f5"), new Guid("28671b9d-62e5-cdb7-3d9c-c825a4e523d6"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Calles anegadas, crecientes y desbordamientos.", "es-CO", "Inundación", null },
                    { new Guid("dddf1838-cb96-9bd2-b8b2-99fba4728fb3"), new Guid("839bd530-255b-36d0-fd39-c9334172d621"), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Incendios, humo o explosiones.", "es-CO", "Incendio", null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_author_id_created_at",
                table: "reports",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_city_id_status",
                table: "reports",
                columns: new[] { "city_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_status_expires_at",
                table: "reports",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.Sql("""
                UPDATE report_media AS media
                SET owner_user_id = report.author_id
                FROM reports AS report
                WHERE media.report_id = report.id
                  AND media.owner_user_id = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_report_media_owner_user_id",
                table: "report_media",
                column: "owner_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_report_media_owner_user_id",
                table: "report_media",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_report_media_owner_user_id",
                table: "report_media");

            migrationBuilder.DropIndex(
                name: "ix_reports_author_id_created_at",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_city_id_status",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_status_expires_at",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_report_media_owner_user_id",
                table: "report_media");

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("1ebcc22d-ad6c-86b7-e963-8d2087157fc1"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("2f92942a-bbe0-ecfc-a451-8115678d164c"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("42925d6a-4c74-2870-7698-9acc3a09d232"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("440d0708-b772-7936-a382-1b32ab8fc93c"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("47646830-7260-864a-8747-4c455afd27fb"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("586f102d-b247-fb01-652c-ea2e1cae24a4"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("67ae33a0-b736-406c-7b56-30a6ca5561a9"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("96e621c4-6abd-c66d-b7e7-d3e8aea34ea7"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("983f528d-a6b7-6e77-6982-04b7d3aa210d"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("9b0307aa-aefa-eca4-d2f7-9f773dd6b375"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("d0f5a633-6dae-a29a-76c4-9358294d37f5"));

            migrationBuilder.DeleteData(
                table: "report_category_translations",
                keyColumn: "id",
                keyValue: new Guid("dddf1838-cb96-9bd2-b8b2-99fba4728fb3"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("299ad7d3-1684-a379-8afe-c0eaec3c5672"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("36ce246e-89eb-34b6-596d-491bfe5204c5"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("49d155ed-dc35-1873-210d-f1b359285f50"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("54b1448c-e468-4228-f918-253bbef3988b"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("56be27e0-278c-1ad7-4bde-746657f1b749"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("838da7e2-a185-60c6-dbd3-0867c5bda4fd"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("b2940ee3-8bc6-0e79-a434-f503cfbe747c"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("b9f7f0f4-15ff-1dde-0f03-62118010e53b"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("d1fe7f63-d6e0-f77c-5cc3-94c70071f355"));

            migrationBuilder.DeleteData(
                table: "system_settings",
                keyColumn: "id",
                keyValue: new Guid("fbb88722-e86e-e028-2f51-533f20ee0dfb"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("28671b9d-62e5-cdb7-3d9c-c825a4e523d6"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("3861576d-40b5-9671-fce7-260f1fc63e98"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("546ba84a-00b0-6347-276f-36084cec1fa5"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("55313b4b-bfcb-9343-4b29-2d7cd06cea72"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("5ac7e05f-5847-7558-956c-063441e1db3b"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("839bd530-255b-36d0-fd39-c9334172d621"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("83cb3751-e08a-7f93-d36a-518e809adfbd"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("84757588-1679-13d9-8580-e6b189f51307"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("8e0cf07e-5a2a-04b8-eb81-107d47d4b7ad"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("c9238d94-25ec-683c-26b0-7246af41e7d5"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("d4b90458-fde7-f8b2-9696-130c76805d82"));

            migrationBuilder.DeleteData(
                table: "report_categories",
                keyColumn: "id",
                keyValue: new Guid("e9c62b0b-a002-63b6-5d01-084e5ea731ac"));

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "report_media");

            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "report_media",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "report_categories",
                columns: new[] { "id", "button_icon_url", "code", "color_hex", "created_at", "description", "importance_weight", "is_active", "is_adult_only", "map_icon_url", "media_requirement", "name", "parent_category_id", "sort_order", "ttl_minutes", "updated_at" },
                values: new object[,]
                {
                    { new Guid("4c9c8d83-0acf-4a06-a01a-741463642574"), "/icons/categories/fire-button.svg", "fire", "#F4511E", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Incendios, humo o explosiones.", 9.0, true, false, "/icons/categories/fire-map.svg", "PhotoOrVideoRequired", "Incendio", null, 6, 180, null },
                    { new Guid("64d82747-b8d5-a72e-d1e5-70190a90ea12"), "/icons/categories/flood-button.svg", "flood", "#039BE5", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Calles anegadas, crecientes y desbordamientos.", 8.0, true, false, "/icons/categories/flood-map.svg", "PhotoOrVideoRequired", "Inundación", null, 7, 720, null },
                    { new Guid("6f916ffa-7888-abc4-94b5-3369f7955b9a"), "/icons/categories/garbage-button.svg", "garbage", "#43A047", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Basura acumulada, escombros o vertimientos.", 3.0, true, false, "/icons/categories/garbage-map.svg", "PhotoRequired", "Basuras", null, 4, 1440, null },
                    { new Guid("719d1e3b-7764-63f5-eb4e-3391cfedd76e"), "/icons/categories/traffic_accident-button.svg", "traffic_accident", "#E53935", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Choques, atropellos y vehículos varados.", 8.0, true, false, "/icons/categories/traffic_accident-map.svg", "PhotoOrVideoRequired", "Accidente de tránsito", null, 1, 180, null },
                    { new Guid("7ff00faf-5a30-f419-d5f3-25240c9be956"), "/icons/categories/other-button.svg", "other", "#757575", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cualquier otra novedad en la calle.", 2.0, true, false, "/icons/categories/other-map.svg", "None", "Otro", null, 8, 180, null },
                    { new Guid("896328c8-018a-02bf-a488-c3b2906cf314"), "/icons/categories/road_closed-button.svg", "road_closed", "#FB8C00", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cierres, derrumbes y tramos que no se pueden pasar.", 6.0, true, false, "/icons/categories/road_closed-map.svg", "PhotoOrVideoRequired", "Vía cerrada", null, 5, 240, null },
                    { new Guid("9e19ae16-481b-cc89-b257-dc51d75c6c1e"), "/icons/categories/theft-button.svg", "theft", "#8E24AA", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Hurtos que están ocurriendo ahora.", 7.0, true, false, "/icons/categories/theft-map.svg", "PhotoOrVideoRequired", "Hurto", null, 2, 360, null },
                    { new Guid("a3656a6c-4570-8435-65d3-4c8e44c5adf0"), "/icons/categories/checkpoint-button.svg", "checkpoint", "#1E88E5", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Retenes, controles y puestos de vigilancia.", 5.0, true, false, "/icons/categories/checkpoint-map.svg", "None", "Retén", null, 3, 120, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_author_id",
                table: "reports",
                column: "author_id");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "avatar_gallery_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avatar_gallery_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    iso_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    region_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    default_locale = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    default_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    default_time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    distance_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    minimum_age = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    button_icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    map_icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    color_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    ttl_minutes = table.Column<int>(type: "integer", nullable: false),
                    importance_weight = table.Column<double>(type: "double precision", nullable: false),
                    media_requirement = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_adult_only = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    retain_days = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retention_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tier = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    shows_ads = table.Column<bool>(type: "boolean", nullable: false),
                    max_devices = table.Column<int>(type: "integer", nullable: true),
                    max_live_minutes_per_stream = table.Column<int>(type: "integer", nullable: true),
                    max_reports_per_day = table.Column<int>(type: "integer", nullable: true),
                    max_anonymous_posts_per_day = table.Column<int>(type: "integer", nullable: true),
                    max_saved_recordings = table.Column<int>(type: "integer", nullable: true),
                    can_sponsor_pins = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "importance_scoring_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_weight_factor = table.Column<double>(type: "double precision", nullable: false),
                    confirmation_factor = table.Column<double>(type: "double precision", nullable: false),
                    comment_factor = table.Column<double>(type: "double precision", nullable: false),
                    reaction_factor = table.Column<double>(type: "double precision", nullable: false),
                    reputation_factor = table.Column<double>(type: "double precision", nullable: false),
                    media_bonus = table.Column<double>(type: "double precision", nullable: false),
                    half_life_minutes = table.Column<double>(type: "double precision", nullable: false),
                    promotion_threshold = table.Column<double>(type: "double precision", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importance_scoring_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_importance_scoring_configs_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: true),
                    locale = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_documents_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                    table.ForeignKey(
                        name: "fk_regions_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_category_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_category_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_category_translations_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_permissions_role_id",
                        column: x => x.role_id,
                        principalTable: "staff_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_prices", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_prices_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plan_prices_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    center = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                    table.ForeignKey(
                        name: "fk_cities_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cities_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    website_url = table.Column<string>(type: "text", nullable: true),
                    logo_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                    table.ForeignKey(
                        name: "fk_organizations_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_category_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ttl_minutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_category_overrides", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_category_overrides_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_category_overrides_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_category_overrides_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    user_name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    email_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    guardian_consent_granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    lockout_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_password_change_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_secret = table.Column<string>(type: "text", nullable: true),
                    suspended_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deletion_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_users_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    center = table.Column<Point>(type: "geography(point, 4326)", nullable: true),
                    radius_meters = table.Column<double>(type: "double precision", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_alert_rules_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alert_rules_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alert_rules_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_deletion_requests_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    changes = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    anonymous_index = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_comments_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_priority = table.Column<bool>(type: "boolean", nullable: false),
                    resolved_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complaints", x => x.id);
                    table.ForeignKey(
                        name: "fk_complaints_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_complaints_resolved_by_staff_id",
                        column: x => x.resolved_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_consent_records_country_id",
                        column: x => x.country_id,
                        principalTable: "countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consent_records_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_export_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_export_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_export_requests_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "friend_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friend_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_friend_invitations_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_friend_invitations_inviter_id",
                        column: x => x.inviter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.ForeignKey(
                        name: "fk_friendships_addressee_id",
                        column: x => x.addressee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_friendships_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "guardian_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    notice_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    minor_heard_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    guardian_full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    guardian_document_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    guardian_document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    guardian_contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    guardian_email_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    digital_signature = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    receipt_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    live_streaming_authorized = table.Column<bool>(type: "boolean", nullable: false),
                    location_sharing_authorized = table.Column<bool>(type: "boolean", nullable: false),
                    authorization_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    authorization_user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    declined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guardian_consents", x => x.id);
                    table.ForeignKey(
                        name: "fk_guardian_consents_minor_user_id",
                        column: x => x.minor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    placed_by_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_holds", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_holds_placed_by_staff_id",
                        column: x => x.placed_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_share_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    friend_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_share_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_share_recipients_friend_user_id",
                        column: x => x.friend_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_location_share_recipients_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email_attempted = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    device_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_login_attempts_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preferences_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    push_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    nearby_radius_meters = table.Column<double>(type: "double precision", nullable: false),
                    notify_friend_reports = table.Column<bool>(type: "boolean", nullable: false),
                    notify_friend_lives = table.Column<bool>(type: "boolean", nullable: false),
                    notify_important_news = table.Column<bool>(type: "boolean", nullable: false),
                    quiet_hours_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    quiet_hours_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_settings_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    body = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_organization_members_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_organization_members_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "privacy_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    center = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    radius_meters = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_privacy_zones", x => x.id);
                    table.ForeignKey(
                        name: "fk_privacy_zones_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_reactions_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_tokens_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sponsored_pins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    image_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    link_url = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sponsored_pins", x => x.id);
                    table.ForeignKey(
                        name: "fk_sponsored_pins_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sponsored_pins_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sponsored_pins_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_assignments_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_assignments_role_id",
                        column: x => x.role_id,
                        principalTable: "staff_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_assignments_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscriptions_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_price_id",
                        column: x => x.price_id,
                        principalTable: "plan_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "takedown_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    requester_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    handled_by_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_takedown_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_takedown_requests_handled_by_staff_id",
                        column: x => x.handled_by_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_blocks_blocked_user_id",
                        column: x => x.blocked_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_blocks_blocker_id",
                        column: x => x.blocker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_category_interests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_category_interests", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_category_interests_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_category_interests_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_identifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    device_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    app_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    push_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_devices_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_interest_cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_interest_cities", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_interest_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_interest_cities_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    point = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_locations_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_locations_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    bio = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    avatar_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    avatar_gallery_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_profile_consented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    show_realtime_location = table.Column<bool>(type: "boolean", nullable: false),
                    share_location_with_friends = table.Column<bool>(type: "boolean", nullable: false),
                    friend_location_sharing_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    friend_location_sharing_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ghost_mode_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_report_location = table.Column<Point>(type: "geography(point, 4326)", nullable: true),
                    last_report_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_profiles_avatar_gallery_item_id",
                        column: x => x.avatar_gallery_item_id,
                        principalTable: "avatar_gallery_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_profiles_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_reputations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    confirmed_reports = table.Column<int>(type: "integer", nullable: false),
                    rejected_reports = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_reputations", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_reputations_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "anonymous_identity_access_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_anonymous_identity_access_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_anonymous_identity_access_logs_complaint_id",
                        column: x => x.complaint_id,
                        principalTable: "complaints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_anonymous_identity_access_logs_staff_id",
                        column: x => x.staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_moderation_actions_complaint_id",
                        column: x => x.complaint_id,
                        principalTable: "complaints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_moderation_actions_staff_id",
                        column: x => x.staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sponsored_pin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_sponsored_pin_id",
                        column: x => x.sponsored_pin_id,
                        principalTable: "sponsored_pins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_deliveries_device_id",
                        column: x => x.device_id,
                        principalTable: "user_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_deliveries_notification_id",
                        column: x => x.notification_id,
                        principalTable: "notifications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_replaced_by_token_id",
                        column: x => x.replaced_by_token_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user_device_id",
                        column: x => x.user_device_id,
                        principalTable: "user_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_streams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    streamer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    visibility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    streamer_is_minor = table.Column<bool>(type: "boolean", nullable: false),
                    faces_blurred = table.Column<bool>(type: "boolean", nullable: false),
                    voice_modified = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    peak_viewers = table.Column<int>(type: "integer", nullable: false),
                    ingest_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recording_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recording_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    internal_copy_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    report_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_streams", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_streams_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_streams_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_streams_streamer_id",
                        column: x => x.streamer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_viewers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_viewers", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_viewers_live_stream_id",
                        column: x => x.live_stream_id,
                        principalTable: "live_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_viewers_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<Point>(type: "geography(point, 4326)", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    author_is_minor = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_live_stream_id = table.Column<Guid>(type: "uuid", nullable: true),
                    still_there_count = table.Column<int>(type: "integer", nullable: false),
                    gone_now_count = table.Column<int>(type: "integer", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false),
                    reaction_count = table.Column<int>(type: "integer", nullable: false),
                    is_important = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_source_live_stream_id",
                        column: x => x.source_live_stream_id,
                        principalTable: "live_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    promoted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_pinned_by_staff = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_items_category_id",
                        column: x => x.category_id,
                        principalTable: "report_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_news_items_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_news_items_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_confirmations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    voter_location = table.Column<Point>(type: "geography(point, 4326)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_confirmations", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_confirmations_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_confirmations_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    thumbnail_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    faces_blurred = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_media_report_id",
                        column: x => x.report_id,
                        principalTable: "reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "countries",
                columns: new[] { "id", "default_currency", "default_locale", "default_time_zone", "distance_unit", "is_enabled", "iso_code", "minimum_age", "name", "region_label" },
                values: new object[] { new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), "COP", "es-CO", "America/Bogota", "Kilometers", true, "CO", 14, "Colombia", "Departamento" });

            migrationBuilder.InsertData(
                table: "report_categories",
                columns: new[] { "id", "button_icon_url", "code", "color_hex", "description", "importance_weight", "is_active", "is_adult_only", "map_icon_url", "media_requirement", "name", "parent_category_id", "sort_order", "ttl_minutes" },
                values: new object[,]
                {
                    { new Guid("4c9c8d83-0acf-4a06-a01a-741463642574"), "/icons/categories/fire-button.svg", "fire", "#F4511E", "Incendios, humo o explosiones.", 9.0, true, false, "/icons/categories/fire-map.svg", "PhotoOrVideoRequired", "Incendio", null, 6, 180 },
                    { new Guid("64d82747-b8d5-a72e-d1e5-70190a90ea12"), "/icons/categories/flood-button.svg", "flood", "#039BE5", "Calles anegadas, crecientes y desbordamientos.", 8.0, true, false, "/icons/categories/flood-map.svg", "PhotoOrVideoRequired", "Inundación", null, 7, 720 },
                    { new Guid("6f916ffa-7888-abc4-94b5-3369f7955b9a"), "/icons/categories/garbage-button.svg", "garbage", "#43A047", "Basura acumulada, escombros o vertimientos.", 3.0, true, false, "/icons/categories/garbage-map.svg", "PhotoRequired", "Basuras", null, 4, 1440 },
                    { new Guid("719d1e3b-7764-63f5-eb4e-3391cfedd76e"), "/icons/categories/traffic_accident-button.svg", "traffic_accident", "#E53935", "Choques, atropellos y vehículos varados.", 8.0, true, false, "/icons/categories/traffic_accident-map.svg", "PhotoOrVideoRequired", "Accidente de tránsito", null, 1, 180 },
                    { new Guid("7ff00faf-5a30-f419-d5f3-25240c9be956"), "/icons/categories/other-button.svg", "other", "#757575", "Cualquier otra novedad en la calle.", 2.0, true, false, "/icons/categories/other-map.svg", "None", "Otro", null, 8, 180 },
                    { new Guid("896328c8-018a-02bf-a488-c3b2906cf314"), "/icons/categories/road_closed-button.svg", "road_closed", "#FB8C00", "Cierres, derrumbes y tramos que no se pueden pasar.", 6.0, true, false, "/icons/categories/road_closed-map.svg", "PhotoOrVideoRequired", "Vía cerrada", null, 5, 240 },
                    { new Guid("9e19ae16-481b-cc89-b257-dc51d75c6c1e"), "/icons/categories/theft-button.svg", "theft", "#8E24AA", "Hurtos que están ocurriendo ahora.", 7.0, true, false, "/icons/categories/theft-map.svg", "PhotoOrVideoRequired", "Hurto", null, 2, 360 },
                    { new Guid("a3656a6c-4570-8435-65d3-4c8e44c5adf0"), "/icons/categories/checkpoint-button.svg", "checkpoint", "#1E88E5", "Retenes, controles y puestos de vigilancia.", 5.0, true, false, "/icons/categories/checkpoint-map.svg", "None", "Retén", null, 3, 120 }
                });

            migrationBuilder.InsertData(
                table: "staff_roles",
                columns: new[] { "id", "description", "is_system", "name" },
                values: new object[,]
                {
                    { new Guid("31329bee-f304-03d3-7cd1-998cd2ac5280"), "Administra planes y consulta la auditoría.", true, "Finanzas" },
                    { new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e"), "Acceso total al panel, incluida la gestión del equipo.", true, "Super admin" },
                    { new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e"), "Atiende cuentas, quejas y solicitudes de retiro.", true, "Soporte" },
                    { new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd"), "Modera contenido, cortes de vivos, quejas y anonimato.", true, "Moderador" }
                });

            migrationBuilder.InsertData(
                table: "subscription_plans",
                columns: new[] { "id", "can_sponsor_pins", "code", "description", "is_active", "max_anonymous_posts_per_day", "max_devices", "max_live_minutes_per_stream", "max_reports_per_day", "max_saved_recordings", "name", "shows_ads", "sort_order", "tier" },
                values: new object[,]
                {
                    { new Guid("3807a8b2-dfec-9921-840f-afb11fac34b4"), true, "advanced", "Sin topes de uso y con pines patrocinados.", true, null, null, null, null, null, "Advanced", false, 3, "Advanced" },
                    { new Guid("3828a36e-bb85-e023-67c7-9beedcadd9d4"), false, "free", "Mapa, reportes y vivos con publicidad. Hasta 2 dispositivos.", true, 3, 2, 15, 10, 0, "Free", true, 0, "Free" },
                    { new Guid("3c441ba4-877b-dd2d-d809-44ebe9b5cd00"), true, "pro", "Reportes y anónimos sin tope, y pines patrocinados.", true, null, 5, 180, null, 30, "Pro", false, 2, "Pro" },
                    { new Guid("eb212694-4727-12af-62cd-89991754e00c"), false, "basic", "Sin publicidad, con más reportes y vivos más largos.", true, 15, 3, 60, 30, 5, "Basic", false, 1, "Basic" }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "description", "key", "value" },
                values: new object[,]
                {
                    { new Guid("031b2e09-dddb-6ee4-1d47-b2004cf599e5"), "Dispositivos activos permitidos en el plan Free.", "devices.max_free", "2" },
                    { new Guid("1cbdebed-ea22-b9fe-bab1-9f3b10e23f37"), "Radio por defecto de las alertas cercanas, en metros.", "notifications.default_radius_meters", "1000" },
                    { new Guid("1edff7d9-0985-26a3-fb99-064d80feb2ff"), "Nombre visible de quien publica en anónimo.", "anonymity.display_name", "Situero anónimo" },
                    { new Guid("403d29fb-dfbe-3312-0f32-a757fbef61f5"), "Intentos fallidos antes de bloquear el inicio de sesión.", "auth.max_failed_attempts", "5" },
                    { new Guid("66e3e067-052c-5403-f482-d9870e7594ce"), "Días que se conserva la grabación pública de un vivo.", "live.recording_retention_days", "60" },
                    { new Guid("6e4b99df-aecf-57c2-311c-d9fc2f21ea86"), "Días extra que se conserva la copia interna de un vivo.", "live.internal_copy_retention_days", "7" },
                    { new Guid("c64962f0-11b8-c620-4c45-8cdaf1b0a7b0"), "Días que se conserva un reporte.", "report.retention_days", "365" },
                    { new Guid("cfb8a198-a285-4174-122d-d88195262fdc"), "Minutos que dura el bloqueo por intentos fallidos.", "auth.lockout_minutes", "15" }
                });

            migrationBuilder.InsertData(
                table: "regions",
                columns: new[] { "id", "code", "country_id", "is_enabled", "name" },
                values: new object[,]
                {
                    { new Guid("0b5d3b2f-85f3-783f-2533-9d33c32dd566"), "15", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Boyacá" },
                    { new Guid("0c24c990-fd4e-0c3d-b85a-cdbfd739da70"), "13", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Bolívar" },
                    { new Guid("0c72f36a-369d-e53c-5bd5-d2cc2512e317"), "99", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Vichada" },
                    { new Guid("0cc9d739-6aa5-c86d-6c5b-fade76dfed7d"), "70", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Sucre" },
                    { new Guid("0eb4d5eb-f086-d3d7-7538-499f51fbc627"), "73", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Tolima" },
                    { new Guid("0ec120ad-7cce-23c3-51dc-60b944a3b5a7"), "23", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Córdoba" },
                    { new Guid("1f36d307-7dc0-2518-fba8-ccb1d1d08668"), "86", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Putumayo" },
                    { new Guid("21273e28-1d3f-b6c9-9c2b-95fa64811466"), "76", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Valle del Cauca" },
                    { new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "05", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Antioquia" },
                    { new Guid("279c5ba9-8b5a-3513-078c-e364da9eb5d1"), "41", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Huila" },
                    { new Guid("3455b533-58a9-ff82-ef9f-dc2100865a5d"), "20", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cesar" },
                    { new Guid("36c708ed-9674-981f-46bb-70f008b41c72"), "52", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Nariño" },
                    { new Guid("39121184-a150-2e7f-98d2-cf458f56b0f2"), "18", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Caquetá" },
                    { new Guid("473026f9-032e-6bc9-df5f-a419f9e417e6"), "27", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Chocó" },
                    { new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "25", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cundinamarca" },
                    { new Guid("5b3bd4fc-7292-e2c4-d1dc-a868ec449177"), "54", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Norte de Santander" },
                    { new Guid("5d27f156-7ae6-0b70-4e51-c6674c69ebeb"), "63", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Quindío" },
                    { new Guid("66057ae6-5295-d6ae-4034-88627a9b7006"), "95", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Guaviare" },
                    { new Guid("67fcf942-6063-8945-6e38-7f8b01f59706"), "08", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Atlántico" },
                    { new Guid("6a873ff0-9271-6668-69f5-6beb890c9937"), "47", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Magdalena" },
                    { new Guid("79df6821-1ea9-2704-47d3-63342bce3d16"), "17", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Caldas" },
                    { new Guid("7e3fcde9-4e84-0443-31c9-9161dda35e65"), "97", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Vaupés" },
                    { new Guid("8803a063-5bc1-97c9-abbd-843c4b266a17"), "11", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Bogotá, D.C." },
                    { new Guid("a19c8681-7273-4d4d-2e8b-7a6a84e58398"), "88", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "San Andrés y Providencia" },
                    { new Guid("a30e8585-1147-5dd1-6d20-a79e078883fc"), "91", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Amazonas" },
                    { new Guid("a5816621-d4cd-dafa-c636-22f8d9ff70d8"), "81", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Arauca" },
                    { new Guid("b694f73d-9e47-9988-fb74-c7bfa1230b24"), "50", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Meta" },
                    { new Guid("bae54c42-f437-518e-fce7-6861df95730d"), "68", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Santander" },
                    { new Guid("bc25dd79-d128-365c-1fe9-e35ed10a680e"), "94", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Guainía" },
                    { new Guid("c356b3d5-468c-cfb2-6505-f0634fd59825"), "66", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Risaralda" },
                    { new Guid("cd2f7e81-d7cb-667e-e8d9-252569cb5540"), "85", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Casanare" },
                    { new Guid("df060b9c-e623-130b-eb98-b281c7faf785"), "44", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "La Guajira" },
                    { new Guid("e5a63cd1-96bd-bff8-3793-c6e19e25ac00"), "19", new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cauca" }
                });

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "id", "code", "role_id" },
                values: new object[,]
                {
                    { new Guid("017cd24a-7d6c-ce20-af7d-64b377c9ab69"), "users.view", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("0594afe7-a2fc-91a0-f734-d40388bf85a9"), "users.suspend", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("1c99d0bd-8d64-3518-47d9-9086925ecc0f"), "organizations.verify", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("1dd1ecff-11fd-9fb0-a895-12b014fd631b"), "categories.manage", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("1e72c0ec-c721-b535-62bb-697f8c7a1276"), "categories.manage", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("20217631-e277-8f00-92c6-514765e55a4e"), "audit.view", new Guid("31329bee-f304-03d3-7cd1-998cd2ac5280") },
                    { new Guid("2e7ee9eb-dbb6-57e6-7bd8-09a0a8774ae1"), "audit.view", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("46164879-24e5-5285-7a2e-2b0f3c936228"), "live.cut", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("60437591-f054-dc4a-ed6a-55bf916c144f"), "users.view", new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e") },
                    { new Guid("64b88447-6ffd-56e3-7263-063c415c62d1"), "live.cut", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("6c13a445-b745-4205-3107-b9ac3efb7fc6"), "takedowns.handle", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("74681ca7-95ec-7a38-df15-73fbdd532440"), "plans.manage", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("7cfc4ae2-77fc-8213-18c7-4e13e955995a"), "audit.view", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("80464c25-6dfc-b262-fbca-0bd0e19e6b8f"), "takedowns.handle", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("838fb134-c96a-fca7-1bb1-21117b8997b6"), "complaints.resolve", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("966907c2-1088-f250-a38d-d4c20f282b09"), "settings.manage", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("99024685-53d0-5a97-a1b0-5449db059959"), "takedowns.handle", new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e") },
                    { new Guid("9bc6be02-8edb-275f-c434-3d1666b0a9fb"), "complaints.resolve", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("a97b0851-3c7e-036c-ad53-e5b4a6e03a23"), "staff.manage", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("ae2eb5e3-c40f-bf8e-af63-c5ae9d127107"), "anonymity.reveal", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("b3851540-18bd-1157-06ff-d21ae57bcce7"), "organizations.verify", new Guid("d879da60-b0bc-10d2-8b05-2a4950bc41bd") },
                    { new Guid("cd6f4437-feeb-43d0-c42c-10067498ea04"), "plans.manage", new Guid("31329bee-f304-03d3-7cd1-998cd2ac5280") },
                    { new Guid("d96ad0dc-bf01-6b5c-8549-8a029f7fe484"), "anonymity.reveal", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("f19d0beb-ec3e-a8c0-4104-648e2649f829"), "complaints.resolve", new Guid("d4803507-bbd5-9420-5b83-d889ec205b0e") },
                    { new Guid("f1b4f586-24fe-9b8d-ea01-45eed06f33cd"), "users.view", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") },
                    { new Guid("f563e9c6-69f0-28e5-7cdc-b17a41989a81"), "users.suspend", new Guid("97fc5ccd-53a5-78f4-4699-624339d1c72e") }
                });

            migrationBuilder.InsertData(
                table: "cities",
                columns: new[] { "id", "center", "country_id", "is_enabled", "name", "region_id", "time_zone" },
                values: new object[,]
                {
                    { new Guid("035a07cb-a0ae-ff38-002e-8b62e193d049"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-77.0312 3.8801)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Buenaventura", new Guid("21273e28-1d3f-b6c9-9c2b-95fa64811466"), "America/Bogota" },
                    { new Guid("0447449f-af0b-34f6-0611-5f5b163b31a3"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.8814 8.7479)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Montería", new Guid("0ec120ad-7cce-23c3-51dc-60b944a3b5a7"), "America/Bogota" },
                    { new Guid("0463f61f-f7eb-362a-2863-f62f1f3a0407"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.532 3.4516)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cali", new Guid("21273e28-1d3f-b6c9-9c2b-95fa64811466"), "America/Bogota" },
                    { new Guid("049b5aee-b44e-1c6f-bb89-82e1e1dc9c4c"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.6147 2.4448)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Popayán", new Guid("e5a63cd1-96bd-bff8-3793-c6e19e25ac00"), "America/Bogota" },
                    { new Guid("05a0a00f-cb09-c181-c6e8-3011fcde6965"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.0721 4.711)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Bogotá", new Guid("8803a063-5bc1-97c9-abbd-843c4b266a17"), "America/Bogota" },
                    { new Guid("1090bb63-7548-b507-869a-3cab6469e15b"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-69.9406 -4.2153)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Leticia", new Guid("a30e8585-1147-5dd1-6d20-a79e078883fc"), "America/Bogota" },
                    { new Guid("1291ac1b-c237-2102-9b9d-a85f7bec5fe4"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.5174 5.0689)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Manizales", new Guid("79df6821-1ea9-2704-47d3-63342bce3d16"), "America/Bogota" },
                    { new Guid("13f82cf1-84b0-6f0d-ab80-16db64cd0462"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-67.9239 3.8653)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Inírida", new Guid("bc25dd79-d128-365c-1fe9-e35ed10a680e"), "America/Bogota" },
                    { new Guid("1ee3abf4-f104-8ff7-713a-fa67dbe407e3"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.0862 7.0622)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Floridablanca", new Guid("bae54c42-f437-518e-fce7-6861df95730d"), "America/Bogota" },
                    { new Guid("263914d8-4360-fa70-b0f6-c59cb2e376c2"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-67.4859 6.189)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Puerto Carreño", new Guid("0c72f36a-369d-e53c-5bd5-d2cc2512e317"), "America/Bogota" },
                    { new Guid("31c50097-ded8-2122-5b48-332f4ce37dce"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-77.2811 1.2136)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Pasto", new Guid("36c708ed-9674-981f-46bb-70f008b41c72"), "America/Bogota" },
                    { new Guid("352a0153-94d5-9515-583c-fecf4da4d303"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.4794 10.391)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cartagena de Indias", new Guid("0c24c990-fd4e-0c3d-b85a-cdbfd739da70"), "America/Bogota" },
                    { new Guid("35c342d7-f0f0-7582-d8d0-07606d4b1274"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.6113 6.1719)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Itagüí", new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "America/Bogota" },
                    { new Guid("3d61ef71-613a-2cee-cdd9-f8b35c321211"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.7696 10.9184)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Soledad", new Guid("67fcf942-6063-8945-6e38-7f8b01f59706"), "America/Bogota" },
                    { new Guid("3dae897d-ef0a-c91c-27c9-94bd615ed5d6"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.1954 4.0847)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Tuluá", new Guid("21273e28-1d3f-b6c9-9c2b-95fa64811466"), "America/Bogota" },
                    { new Guid("3e845d87-29f7-affb-4272-c1abcdb68535"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.6811 4.5339)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Armenia", new Guid("5d27f156-7ae6-0b70-4e51-c6674c69ebeb"), "America/Bogota" },
                    { new Guid("44aff793-5be9-0cf1-b4db-eda167cc0f1b"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-72.6459 2.5729)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "San José del Guaviare", new Guid("66057ae6-5295-d6ae-4034-88627a9b7006"), "America/Bogota" },
                    { new Guid("46a3ff88-13ac-6787-4b7c-eb8de8a7da86"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-70.1735 1.1983)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Mitú", new Guid("7e3fcde9-4e84-0443-31c9-9161dda35e65"), "America/Bogota" },
                    { new Guid("51508908-f8b7-c6bd-bc06-376c321dc8f4"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.583 6.1699)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Envigado", new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "America/Bogota" },
                    { new Guid("59869a1f-1c9d-e714-b9f2-ae528b35dcdd"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.2322 4.4389)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Ibagué", new Guid("0eb4d5eb-f086-d3d7-7538-499f51fbc627"), "America/Bogota" },
                    { new Guid("5bb8535a-4280-6c70-290f-f9383e649fd6"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-72.3959 5.3378)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Yopal", new Guid("cd2f7e81-d7cb-667e-e8d9-252569cb5540"), "America/Bogota" },
                    { new Guid("5c20a928-fd14-7b47-47d2-845ac2c5fcc5"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.6906 4.8087)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Pereira", new Guid("c356b3d5-468c-cfb2-6505-f0634fd59825"), "America/Bogota" },
                    { new Guid("5e9b3cdc-f4d4-4701-a5c5-76cc7861f4bc"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.8547 7.0653)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Barrancabermeja", new Guid("bae54c42-f437-518e-fce7-6861df95730d"), "America/Bogota" },
                    { new Guid("62b12123-0d32-51ba-9065-b5a0a5bf1e45"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.3036 3.5394)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Palmira", new Guid("21273e28-1d3f-b6c9-9c2b-95fa64811466"), "America/Bogota" },
                    { new Guid("76760587-b951-442b-042e-dbfa8444e898"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.3737 6.1554)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Rionegro", new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "America/Bogota" },
                    { new Guid("7b7b441b-ba85-3c5f-985b-f21ea453d296"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-70.7617 7.0903)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Arauca", new Guid("a5816621-d4cd-dafa-c636-22f8d9ff70d8"), "America/Bogota" },
                    { new Guid("7bc4a239-7535-a8cb-04ed-6b84db0654d5"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.3638 4.3365)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Fusagasugá", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("81c0ac87-3ca1-8d35-ee8d-f889c9e1b86a"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.2168 4.5794)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Soacha", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("84e89f3a-4238-5787-1648-17bb00f4982d"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.2532 10.4631)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Valledupar", new Guid("3455b533-58a9-ff82-ef9f-dc2100865a5d"), "America/Bogota" },
                    { new Guid("95791d35-c117-3704-e012-ca6e22ff001f"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.6062 1.6144)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Florencia", new Guid("39121184-a150-2e7f-98d2-cf458f56b0f2"), "America/Bogota" },
                    { new Guid("a1151f53-7400-c236-c378-5509f007070f"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.0048 5.0221)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Zipaquirá", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("aba90586-5c06-7cec-4dda-5bc06094312e"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.0586 4.8613)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Chía", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("b2e79d76-cbc7-edc6-6729-f762e7b83b34"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.5812 6.2442)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Medellín", new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "America/Bogota" },
                    { new Guid("b4991faa-d39f-0278-17ba-b7167bb67e58"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.7813 10.9685)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Barranquilla", new Guid("67fcf942-6063-8945-6e38-7f8b01f59706"), "America/Bogota" },
                    { new Guid("b4d8c930-3b59-f1b7-71bd-cedb027004af"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-72.9072 11.5444)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Riohacha", new Guid("df060b9c-e623-130b-eb98-b281c7faf785"), "America/Bogota" },
                    { new Guid("b67c57eb-fc57-f237-8ca6-7e21e156865e"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.652 1.1528)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Mocoa", new Guid("1f36d307-7dc0-2518-fba8-ccb1d1d08668"), "America/Bogota" },
                    { new Guid("bb2cb053-738b-f189-b19f-7337ee7f5701"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.3978 9.3047)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Sincelejo", new Guid("0cc9d739-6aa5-c86d-6c5b-fade76dfed7d"), "America/Bogota" },
                    { new Guid("bc9cbd03-44eb-2cb4-5189-3f96b92eaa40"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-76.6611 5.6947)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Quibdó", new Guid("473026f9-032e-6bc9-df5f-a419f9e417e6"), "America/Bogota" },
                    { new Guid("be94d8ea-01f6-a0e5-b7a9-c5f0b1013344"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.3678 5.5353)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Tunja", new Guid("0b5d3b2f-85f3-783f-2533-9d33c32dd566"), "America/Bogota" },
                    { new Guid("bf317192-ab17-22e2-8647-3f2399754654"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.8008 4.303)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Girardot", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("cef4b7d5-79c7-0f4f-6cd8-60bd479c8088"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.199 11.2408)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Santa Marta", new Guid("6a873ff0-9271-6668-69f5-6beb890c9937"), "America/Bogota" },
                    { new Guid("d3a87f8a-9b96-f76c-93d9-d41e2dce3947"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-81.7006 12.5847)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "San Andrés", new Guid("a19c8681-7273-4d4d-2e8b-7a6a84e58398"), "America/Bogota" },
                    { new Guid("daec37c5-b3a5-e61d-7ad0-4b7348a131db"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-74.3548 4.8146)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Facatativá", new Guid("4a8ff199-3315-0b3f-9d68-ed4ba5f6cece"), "America/Bogota" },
                    { new Guid("dd9e080c-a5a7-ee76-28e9-48f295cca4b7"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.1227 7.1193)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Bucaramanga", new Guid("bae54c42-f437-518e-fce7-6861df95730d"), "America/Bogota" },
                    { new Guid("e212426b-146d-9d95-d67c-a9906a9e6c43"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-72.5078 7.8939)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Cúcuta", new Guid("5b3bd4fc-7292-e2c4-d1dc-a868ec449177"), "America/Bogota" },
                    { new Guid("e346c782-d49e-a1e2-01cc-1dfa975d997c"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-73.6266 4.142)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Villavicencio", new Guid("b694f73d-9e47-9988-fb74-c7bfa1230b24"), "America/Bogota" },
                    { new Guid("f0327b03-13f4-c07b-d101-fa1b60909ed6"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.558 6.3373)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Bello", new Guid("213db0c0-2ddc-a69a-64dd-7e27c9064eb9"), "America/Bogota" },
                    { new Guid("f46950d4-bdaa-6afb-3a84-0d88cf5c54b8"), (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read("SRID=4326;POINT (-75.2819 2.9273)"), new Guid("64ad145f-12fa-a01d-eb52-19e907a31ad2"), true, "Neiva", new Guid("279c5ba9-8b5a-3513-078c-e364da9eb5d1"), "America/Bogota" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_deletion_requests_user_id",
                table: "account_deletion_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_category_id",
                table: "alert_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_center",
                table: "alert_rules",
                column: "center")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_city_id",
                table: "alert_rules",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_organization_id",
                table: "alert_rules",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_anonymous_identity_access_logs_complaint_id",
                table: "anonymous_identity_access_logs",
                column: "complaint_id");

            migrationBuilder.CreateIndex(
                name: "ix_anonymous_identity_access_logs_staff_id",
                table: "anonymous_identity_access_logs",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_user_id",
                table: "audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_center",
                table: "cities",
                column: "center")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_cities_country_id",
                table: "cities",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_cities_region_id_name",
                table: "cities",
                columns: new[] { "region_id", "name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_id",
                table: "comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_comment_id",
                table: "comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_reporter_id",
                table: "complaints",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_resolved_by_staff_id",
                table: "complaints",
                column: "resolved_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_country_id",
                table: "consent_records",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_user_id",
                table: "consent_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_countries_iso_code",
                table: "countries",
                column: "iso_code",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_data_export_requests_user_id",
                table: "data_export_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_invitations_accepted_by_user_id",
                table: "friend_invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_invitations_inviter_id",
                table: "friend_invitations",
                column: "inviter_id");

            migrationBuilder.CreateIndex(
                name: "ux_friend_invitations_token_hash",
                table: "friend_invitations",
                column: "token_hash",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_friendships_addressee_id",
                table: "friendships",
                column: "addressee_id");

            migrationBuilder.CreateIndex(
                name: "ux_friendships_requester_id_addressee_id",
                table: "friendships",
                columns: new[] { "requester_id", "addressee_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_guardian_consents_minor_user_id",
                table: "guardian_consents",
                column: "minor_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_guardian_consents_token_hash",
                table: "guardian_consents",
                column: "token_hash",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_importance_scoring_configs_country_id",
                table: "importance_scoring_configs",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ix_legal_documents_country_id",
                table: "legal_documents",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_legal_documents_type_country_id_locale_version",
                table: "legal_documents",
                columns: new[] { "type", "country_id", "locale", "version" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_legal_holds_placed_by_staff_id",
                table: "legal_holds",
                column: "placed_by_staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_category_id",
                table: "live_streams",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_city_id",
                table: "live_streams",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_location",
                table: "live_streams",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_report_id",
                table: "live_streams",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_streams_streamer_id",
                table: "live_streams",
                column: "streamer_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_viewers_live_stream_id",
                table: "live_viewers",
                column: "live_stream_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_viewers_user_id",
                table: "live_viewers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_location_share_recipients_friend_user_id",
                table: "location_share_recipients",
                column: "friend_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_location_share_recipients_user_id_friend_user_id",
                table: "location_share_recipients",
                columns: new[] { "user_id", "friend_user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_login_attempts_user_id_occurred_at",
                table: "login_attempts",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_complaint_id",
                table: "moderation_actions",
                column: "complaint_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_staff_id",
                table: "moderation_actions",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_category_id",
                table: "news_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_city_id",
                table: "news_items",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_report_id",
                table: "news_items",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_device_id",
                table: "notification_deliveries",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_deliveries_notification_id",
                table: "notification_deliveries",
                column: "notification_id");

            migrationBuilder.CreateIndex(
                name: "ux_notification_preferences_user_id_type_channel",
                table: "notification_preferences",
                columns: new[] { "user_id", "type", "channel" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_notification_settings_user_id",
                table: "notification_settings",
                column: "user_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id",
                table: "notifications",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_members_user_id",
                table: "organization_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_organization_members_organization_id_user_id",
                table: "organization_members",
                columns: new[] { "organization_id", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_city_id",
                table: "organizations",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ux_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_payments_sponsored_pin_id",
                table: "payments",
                column: "sponsored_pin_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_subscription_id",
                table: "payments",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_user_id",
                table: "payments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_prices_country_id",
                table: "plan_prices",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_plan_prices_plan_id_period_currency_code_country_id",
                table: "plan_prices",
                columns: new[] { "plan_id", "period", "currency_code", "country_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_privacy_zones_center",
                table: "privacy_zones",
                column: "center")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_privacy_zones_user_id",
                table: "privacy_zones",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reactions_user_id",
                table: "reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_reactions_target_type_target_id_user_id",
                table: "reactions",
                columns: new[] { "target_type", "target_id", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_token_id",
                table: "refresh_tokens",
                column: "replaced_by_token_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_device_id",
                table: "refresh_tokens",
                column: "user_device_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_regions_country_id_code",
                table: "regions",
                columns: new[] { "country_id", "code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_regions_country_id_name",
                table: "regions",
                columns: new[] { "country_id", "name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_report_categories_parent_category_id",
                table: "report_categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ux_report_categories_code",
                table: "report_categories",
                column: "code",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_report_category_overrides_category_id",
                table: "report_category_overrides",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_category_overrides_city_id",
                table: "report_category_overrides",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_category_overrides_country_id",
                table: "report_category_overrides",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_report_category_translations_category_id_locale",
                table: "report_category_translations",
                columns: new[] { "category_id", "locale" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_report_confirmations_user_id",
                table: "report_confirmations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_confirmations_voter_location",
                table: "report_confirmations",
                column: "voter_location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_report_confirmations_report_id_user_id",
                table: "report_confirmations",
                columns: new[] { "report_id", "user_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_report_media_report_id",
                table: "report_media",
                column: "report_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_author_id",
                table: "reports",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_category_id",
                table: "reports",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_city_id_status_expires_at",
                table: "reports",
                columns: new[] { "city_id", "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_location",
                table: "reports",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_reports_source_live_stream_id",
                table: "reports",
                column: "source_live_stream_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_tokens_user_id",
                table: "security_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_security_tokens_token_hash",
                table: "security_tokens",
                column: "token_hash",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_pins_city_id",
                table: "sponsored_pins",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_pins_location",
                table: "sponsored_pins",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_pins_organization_id",
                table: "sponsored_pins",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_sponsored_pins_owner_user_id",
                table: "sponsored_pins",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_assignments_granted_by_user_id",
                table: "staff_assignments",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_assignments_role_id",
                table: "staff_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_assignments_user_id",
                table: "staff_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_staff_permissions_role_id_code",
                table: "staff_permissions",
                columns: new[] { "role_id", "code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_staff_roles_name",
                table: "staff_roles",
                column: "name",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_subscription_plans_code",
                table: "subscription_plans",
                column: "code",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_plan_id",
                table: "subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_price_id",
                table: "subscriptions",
                column: "price_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_user_id",
                table: "subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_system_settings_key",
                table: "system_settings",
                column: "key",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_takedown_requests_handled_by_staff_id",
                table: "takedown_requests",
                column: "handled_by_staff_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_user_category_interests_category_id",
                table: "user_category_interests",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_category_interests_user_id_category_id",
                table: "user_category_interests",
                columns: new[] { "user_id", "category_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_user_devices_user_id_device_identifier",
                table: "user_devices",
                columns: new[] { "user_id", "device_identifier" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_user_interest_cities_city_id",
                table: "user_interest_cities",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_interest_cities_user_id_city_id",
                table: "user_interest_cities",
                columns: new[] { "user_id", "city_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_user_locations_city_id",
                table: "user_locations",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_locations_point",
                table: "user_locations",
                column: "point")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_user_locations_user_id",
                table: "user_locations",
                column: "user_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_avatar_gallery_item_id",
                table: "user_profiles",
                column: "avatar_gallery_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_last_report_location",
                table: "user_profiles",
                column: "last_report_location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_user_profiles_user_id",
                table: "user_profiles",
                column: "user_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_user_reputations_user_id",
                table: "user_reputations",
                column: "user_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_users_city_id",
                table: "users",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_country_id",
                table: "users",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_region_id",
                table: "users",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ux_users_user_name",
                table: "users",
                column: "user_name",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddForeignKey(
                name: "fk_live_streams_report_id",
                table: "live_streams",
                column: "report_id",
                principalTable: "reports",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_live_streams_streamer_id",
                table: "live_streams");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_author_id",
                table: "reports");

            migrationBuilder.DropForeignKey(
                name: "fk_live_streams_category_id",
                table: "live_streams");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_category_id",
                table: "reports");

            migrationBuilder.DropForeignKey(
                name: "fk_live_streams_city_id",
                table: "live_streams");

            migrationBuilder.DropForeignKey(
                name: "fk_reports_city_id",
                table: "reports");

            migrationBuilder.DropForeignKey(
                name: "fk_live_streams_report_id",
                table: "live_streams");

            migrationBuilder.DropTable(
                name: "account_deletion_requests");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "anonymous_identity_access_logs");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "data_export_requests");

            migrationBuilder.DropTable(
                name: "friend_invitations");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "guardian_consents");

            migrationBuilder.DropTable(
                name: "importance_scoring_configs");

            migrationBuilder.DropTable(
                name: "legal_documents");

            migrationBuilder.DropTable(
                name: "legal_holds");

            migrationBuilder.DropTable(
                name: "live_viewers");

            migrationBuilder.DropTable(
                name: "location_share_recipients");

            migrationBuilder.DropTable(
                name: "login_attempts");

            migrationBuilder.DropTable(
                name: "moderation_actions");

            migrationBuilder.DropTable(
                name: "news_items");

            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "organization_members");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "privacy_zones");

            migrationBuilder.DropTable(
                name: "reactions");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "report_category_overrides");

            migrationBuilder.DropTable(
                name: "report_category_translations");

            migrationBuilder.DropTable(
                name: "report_confirmations");

            migrationBuilder.DropTable(
                name: "report_media");

            migrationBuilder.DropTable(
                name: "retention_policies");

            migrationBuilder.DropTable(
                name: "security_tokens");

            migrationBuilder.DropTable(
                name: "staff_assignments");

            migrationBuilder.DropTable(
                name: "staff_permissions");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "takedown_requests");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_category_interests");

            migrationBuilder.DropTable(
                name: "user_interest_cities");

            migrationBuilder.DropTable(
                name: "user_locations");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "user_reputations");

            migrationBuilder.DropTable(
                name: "complaints");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "sponsored_pins");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "user_devices");

            migrationBuilder.DropTable(
                name: "staff_roles");

            migrationBuilder.DropTable(
                name: "avatar_gallery_items");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "plan_prices");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "report_categories");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "countries");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "live_streams");
        }
    }
}

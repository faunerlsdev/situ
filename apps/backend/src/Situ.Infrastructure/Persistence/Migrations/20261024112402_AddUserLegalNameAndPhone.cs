using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Situ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLegalNameAndPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "given_names",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "surnames",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE users SET given_names = 'Pendiente' WHERE given_names = '';
                UPDATE users SET surnames = 'Pendiente' WHERE surnames = '';
                UPDATE users SET phone = '0000000' WHERE phone = '';
                ALTER TABLE users ALTER COLUMN given_names DROP DEFAULT;
                ALTER TABLE users ALTER COLUMN surnames DROP DEFAULT;
                ALTER TABLE users ALTER COLUMN phone DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "given_names",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "surnames",
                table: "users");
        }
    }
}

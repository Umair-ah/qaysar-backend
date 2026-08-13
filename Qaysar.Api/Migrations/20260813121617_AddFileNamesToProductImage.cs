using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaysar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFileNamesToProductImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ProductImages",
                type: "character varying(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "ProductImages",
                type: "character varying(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "ProductImages");
        }
    }
}

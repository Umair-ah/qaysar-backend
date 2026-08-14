using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaysar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNameToQuotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Quotations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Quotations");
        }
    }
}

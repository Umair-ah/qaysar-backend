using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaysar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitPriceToQuotationProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "QuotationProducts",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "QuotationProducts");
        }
    }
}

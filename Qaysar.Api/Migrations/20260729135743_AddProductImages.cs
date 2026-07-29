using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qaysar.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" });

            // Carry forward any existing single ImageUrl into the new table before dropping the column.
            migrationBuilder.Sql(@"
                INSERT INTO ProductImages (ProductId, Url, SortOrder)
                SELECT Id, ImageUrl, 0 FROM Products
                WHERE ImageUrl IS NOT NULL AND LTRIM(RTRIM(ImageUrl)) <> '';
            ");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE p
                SET p.ImageUrl = pi.Url
                FROM Products p
                CROSS APPLY (
                    SELECT TOP 1 Url FROM ProductImages
                    WHERE ProductId = p.Id
                    ORDER BY SortOrder
                ) pi;
            ");

            migrationBuilder.DropTable(
                name: "ProductImages");
        }
    }
}

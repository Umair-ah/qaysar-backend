using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class ProductExportService : IProductExportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProductExportService> _logger;

    public ProductExportService(AppDbContext db, ILogger<ProductExportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        // Single query with every child/related collection included — avoids N+1 across thousands of products.
        var products = await _db.Products.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.Images)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var brands = await _db.Brands.AsNoTracking().OrderBy(b => b.Id).ToListAsync(ct);
        var categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);

        _logger.LogInformation("Exporting {Count} products to Excel.", products.Count);

        using var workbook = new XLWorkbook();
        WriteProductsSheet(workbook, products);
        WriteLookupSheet(workbook, "Brands", "BrandId", brands.Select(b => (b.Id, b.NameEn, b.NameAr)));
        WriteLookupSheet(workbook, "Categories", "CategoryId", categories.Select(c => (c.Id, c.NameEn, c.NameAr)));

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteProductsSheet(XLWorkbook workbook, List<Product> products)
    {
        var ws = workbook.Worksheets.Add("Products");

        // BrandName / CategoryNames are read-only context columns so an admin can see what an ID
        // currently means without cross-referencing the lookup sheets — they are NOT read on import
        // (only BrandId/CategoryIds are). Kept immediately after their ID column and visually
        // distinguished (italic, grey fill) so it's obvious they're for reference only.
        var headers = new List<string>
        {
            "ProductId", "NameEn", "NameAr", "Sku", "DescriptionEn", "DescriptionAr",
            "InStock", "IsVisible", "CostPrice", "LowPrice", "MediumPrice", "HighPrice",
            "BrandId", "BrandName (reference only)", "CategoryIds", "CategoryNames (reference only)",
        };
        for (var i = 1; i <= 10; i++) headers.Add($"Image{i}");

        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var brandNameCol = headers.IndexOf("BrandName (reference only)") + 1;
        var categoryNamesCol = headers.IndexOf("CategoryNames (reference only)") + 1;

        var row = 2;
        foreach (var p in products)
        {
            var col = 1;
            ws.Cell(row, col++).Value = p.Id;
            ws.Cell(row, col++).Value = p.NameEn;
            ws.Cell(row, col++).Value = p.NameAr;
            ws.Cell(row, col++).Value = p.Sku;
            ws.Cell(row, col++).Value = p.DescriptionEn;
            ws.Cell(row, col++).Value = p.DescriptionAr;
            ws.Cell(row, col++).Value = p.InStock;
            ws.Cell(row, col++).Value = p.IsVisible;
            ws.Cell(row, col++).Value = (double)p.CostPrice;
            ws.Cell(row, col++).Value = (double)p.LowPrice;
            ws.Cell(row, col++).Value = (double)p.MediumPrice;
            ws.Cell(row, col++).Value = (double)p.HighPrice;
            ws.Cell(row, col++).Value = p.BrandId;
            ws.Cell(row, col++).Value = p.Brand is null ? "" : $"{p.Brand.NameEn} / {p.Brand.NameAr}";
            ws.Cell(row, col++).Value = string.Join(",", p.ProductCategories.Select(pc => pc.CategoryId).OrderBy(id => id));
            ws.Cell(row, col++).Value = string.Join(", ",
                p.ProductCategories
                    .OrderBy(pc => pc.CategoryId)
                    .Select(pc => pc.Category is null ? "" : $"{pc.Category.NameEn} / {pc.Category.NameAr}"));

            // Image1..Image10 — filenames only, in SortOrder. Never URLs, never embedded images.
            var orderedFileNames = p.Images.OrderBy(i => i.SortOrder).Select(i => i.OriginalFileName).ToList();
            for (var i = 0; i < 10; i++)
                ws.Cell(row, col++).Value = i < orderedFileNames.Count ? orderedFileNames[i] : "";

            row++;
        }

        if (row > 2)
        {
            var lastRow = row - 1;
            foreach (var col in new[] { brandNameCol, categoryNamesCol })
            {
                var range = ws.Range(2, col, lastRow, col);
                range.Style.Font.Italic = true;
                range.Style.Font.FontColor = XLColor.FromArgb(0x59, 0x59, 0x59);
                range.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF2, 0xF2, 0xF2);
            }
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    /// <summary>Writes a simple Id/NameEn/NameAr reference sheet an admin can use to look up the correct ID to type.</summary>
    private static void WriteLookupSheet(XLWorkbook workbook, string sheetName, string idHeader, IEnumerable<(int Id, string NameEn, string NameAr)> items)
    {
        var ws = workbook.Worksheets.Add(sheetName);

        ws.Cell(1, 1).Value = idHeader;
        ws.Cell(1, 2).Value = "NameEn";
        ws.Cell(1, 3).Value = "NameAr";
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value = item.Id;
            ws.Cell(row, 2).Value = item.NameEn;
            ws.Cell(row, 3).Value = item.NameAr;
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }
}

using System.Globalization;
using ClosedXML.Excel;
using Qaysar.Api.DTOs;

namespace Qaysar.Api.Services;

/// <summary>One row of a parsed Products.xlsx import, prior to any database validation.</summary>
public sealed class ProductImportRow
{
    public int RowNumber { get; init; }
    public int? ProductId { get; set; }
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string Sku { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public bool? InStock { get; set; }
    public bool? IsVisible { get; set; }
    public decimal CostPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal MediumPrice { get; set; }
    public decimal HighPrice { get; set; }
    public int? BrandId { get; set; }
    public List<int> CategoryIds { get; } = new();
    /// <summary>Raw Image1..Image10 cell values, trimmed, empty cells skipped. Order preserved (becomes SortOrder).</summary>
    public List<string> ImageFileNames { get; } = new();
}

/// <summary>
/// Pure, DB-free parsing and field-level validation for the bulk product import Excel file.
/// Row-level parse failures are appended to the shared errors list (row number + message)
/// rather than thrown, so the caller can collect every problem before deciding whether to import.
/// </summary>
public static class ProductImportValidator
{
    public static readonly string[] RequiredHeaders =
    {
        "ProductId", "NameEn", "NameAr", "Sku", "DescriptionEn", "DescriptionAr",
        "CostPrice", "LowPrice", "MediumPrice", "HighPrice", "BrandId", "CategoryIds",
    };

    public static List<ProductImportRow> ParseWorkbook(IXLWorksheet ws, List<ImportValidationErrorDto> errors)
    {
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = ws.Row(1);
        var lastHeaderCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var c = 1; c <= lastHeaderCol; c++)
        {
            var name = headerRow.Cell(c).GetString().Trim();
            if (!string.IsNullOrEmpty(name)) columnIndex[name] = c;
        }

        foreach (var header in RequiredHeaders)
        {
            if (!columnIndex.ContainsKey(header))
                errors.Add(new ImportValidationErrorDto(1, null, $"Missing required column '{header}'."));
        }
        // Column mapping is broken — row-level parsing would be meaningless (wrong cells read).
        if (errors.Count > 0) return new List<ProductImportRow>();

        var imageColumns = new List<int>();
        for (var i = 1; i <= 10; i++)
        {
            if (columnIndex.TryGetValue($"Image{i}", out var idx)) imageColumns.Add(idx);
        }
        columnIndex.TryGetValue("InStock", out var inStockCol);
        columnIndex.TryGetValue("IsVisible", out var isVisibleCol);

        var rows = new List<ProductImportRow>();
        var lastRowUsed = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var r = 2; r <= lastRowUsed; r++)
        {
            var xlRow = ws.Row(r);
            if (xlRow.IsEmpty()) continue;

            string Cell(int col) => xlRow.Cell(col).GetString().Trim();

            var row = new ProductImportRow
            {
                RowNumber = r,
                NameEn = Cell(columnIndex["NameEn"]),
                NameAr = Cell(columnIndex["NameAr"]),
                Sku = Cell(columnIndex["Sku"]),
                DescriptionEn = Cell(columnIndex["DescriptionEn"]),
                DescriptionAr = Cell(columnIndex["DescriptionAr"]),
            };

            // ProductId is optional: blank means "create a new product". A non-blank value that
            // isn't a whole number is still a real error — it's not a valid "create" signal.
            var productIdRaw = Cell(columnIndex["ProductId"]);
            if (!string.IsNullOrWhiteSpace(productIdRaw))
            {
                if (!int.TryParse(productIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var productId))
                    errors.Add(new ImportValidationErrorDto(r, null, $"ProductId must be a whole number if provided (got '{productIdRaw}')."));
                else
                    row.ProductId = productId;
            }

            if (string.IsNullOrWhiteSpace(row.NameEn))
                errors.Add(new ImportValidationErrorDto(r, row.ProductId, "NameEn is required."));
            if (string.IsNullOrWhiteSpace(row.NameAr))
                errors.Add(new ImportValidationErrorDto(r, row.ProductId, "NameAr is required."));
            if (string.IsNullOrWhiteSpace(row.Sku))
                errors.Add(new ImportValidationErrorDto(r, row.ProductId, "Sku is required."));

            row.CostPrice = ParseDecimal(Cell(columnIndex["CostPrice"]), "CostPrice", r, row.ProductId, errors);
            row.LowPrice = ParseDecimal(Cell(columnIndex["LowPrice"]), "LowPrice", r, row.ProductId, errors);
            row.MediumPrice = ParseDecimal(Cell(columnIndex["MediumPrice"]), "MediumPrice", r, row.ProductId, errors);
            row.HighPrice = ParseDecimal(Cell(columnIndex["HighPrice"]), "HighPrice", r, row.ProductId, errors);

            var brandIdRaw = Cell(columnIndex["BrandId"]);
            if (!int.TryParse(brandIdRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var brandId))
                errors.Add(new ImportValidationErrorDto(r, row.ProductId, $"BrandId must be a whole number (got '{brandIdRaw}')."));
            else
                row.BrandId = brandId;

            if (inStockCol > 0)
                row.InStock = ParseBool(Cell(inStockCol), "InStock", r, row.ProductId, errors);
            if (isVisibleCol > 0)
                row.IsVisible = ParseBool(Cell(isVisibleCol), "IsVisible", r, row.ProductId, errors);

            var categoryIdsRaw = Cell(columnIndex["CategoryIds"]);
            if (string.IsNullOrWhiteSpace(categoryIdsRaw))
            {
                errors.Add(new ImportValidationErrorDto(r, row.ProductId, "At least one category is required."));
            }
            else
            {
                foreach (var part in categoryIdsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var catId))
                        row.CategoryIds.Add(catId);
                    else
                        errors.Add(new ImportValidationErrorDto(r, row.ProductId, $"CategoryIds contains a non-numeric value: '{part}'."));
                }
                if (row.CategoryIds.Count == 0)
                    errors.Add(new ImportValidationErrorDto(r, row.ProductId, "At least one category is required."));
            }

            foreach (var col in imageColumns)
            {
                var fileName = xlRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(fileName)) row.ImageFileNames.Add(fileName);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static decimal ParseDecimal(string raw, string columnName, int rowNumber, int? productId, List<ImportValidationErrorDto> errors)
    {
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return value;
        errors.Add(new ImportValidationErrorDto(rowNumber, productId, $"'{columnName}' is not a valid number (got '{raw}')."));
        return 0m;
    }

    private static bool? ParseBool(string raw, string columnName, int rowNumber, int? productId, List<ImportValidationErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => Fail(),
        };

        bool? Fail()
        {
            errors.Add(new ImportValidationErrorDto(rowNumber, productId, $"'{columnName}' must be true/false (got '{raw}')."));
            return null;
        }
    }
}

using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class ProductImportService : IProductImportService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;
    private readonly IZipImageService _zipSvc;
    private readonly ILogger<ProductImportService> _logger;

    public ProductImportService(AppDbContext db, IStorageService storage, IZipImageService zipSvc, ILogger<ProductImportService> logger)
    {
        _db = db;
        _storage = storage;
        _zipSvc = zipSvc;
        _logger = logger;
    }

    /// <summary>
    /// Rows with a ProductId update that existing product. Rows with a blank ProductId create a
    /// new product instead (auto-generated ID). All-or-nothing: if any row fails validation,
    /// nothing is written or uploaded.
    /// </summary>
    public async Task<BulkImportResultDto> ImportAsync(Stream excelStream, Stream? zipStream, CancellationToken ct = default)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(excelStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Bulk product import received an invalid Excel file.");
            return SingleFailure(0, "The uploaded file is not a valid Excel (.xlsx) workbook.");
        }

        using (workbook)
        {
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws is null)
                return SingleFailure(0, "The Excel workbook has no worksheets.");

            var errors = new List<ImportValidationErrorDto>();
            var rows = ProductImportValidator.ParseWorkbook(ws, errors);

            if (errors.Count > 0)
                return new BulkImportResultDto(false, rows.Count, 0, 0, errors);

            // Duplicate ProductIds inside Excel (only applies to rows updating an existing product).
            foreach (var group in rows.Where(r => r.ProductId.HasValue).GroupBy(r => r.ProductId!.Value))
            {
                if (group.Count() <= 1) continue;
                var rowNumbers = string.Join(", ", group.Select(r => r.RowNumber));
                errors.Add(new ImportValidationErrorDto(group.First().RowNumber, group.Key,
                    $"Duplicate ProductId {group.Key} found in rows {rowNumbers}."));
            }

            // Duplicate Sku inside Excel — blank Skus are already flagged as required by the row parser.
            foreach (var group in rows.Where(r => !string.IsNullOrWhiteSpace(r.Sku)).GroupBy(r => r.Sku, StringComparer.Ordinal))
            {
                if (group.Count() <= 1) continue;
                var rowNumbers = string.Join(", ", group.Select(r => r.RowNumber));
                errors.Add(new ImportValidationErrorDto(group.First().RowNumber, null,
                    $"Duplicate Sku '{group.Key}' found in rows {rowNumbers}."));
            }

            // Batch existence checks — one round-trip per entity type instead of per row.
            var productIds = rows.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().ToList();
            var brandIds = rows.Where(r => r.BrandId.HasValue).Select(r => r.BrandId!.Value).Distinct().ToList();
            var categoryIds = rows.SelectMany(r => r.CategoryIds).Distinct().ToList();
            var skus = rows.Where(r => !string.IsNullOrWhiteSpace(r.Sku)).Select(r => r.Sku).Distinct().ToList();

            var existingProductIds = (await _db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
            var existingBrandIds = (await _db.Brands.Where(b => brandIds.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct)).ToHashSet();
            var existingCategoryIds = (await _db.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
            // Sku has a unique index — map every colliding Sku already in the DB to its owning ProductId,
            // so an update row that keeps its own Sku unchanged isn't flagged as a false conflict.
            var skuOwners = (await _db.Products.Where(p => skus.Contains(p.Sku)).Select(p => new { p.Id, p.Sku }).ToListAsync(ct))
                .ToDictionary(x => x.Sku, x => x.Id);

            foreach (var row in rows)
            {
                if (row.ProductId.HasValue && !existingProductIds.Contains(row.ProductId.Value))
                    errors.Add(new ImportValidationErrorDto(row.RowNumber, row.ProductId, $"Product {row.ProductId} was not found."));

                if (row.BrandId.HasValue && !existingBrandIds.Contains(row.BrandId.Value))
                    errors.Add(new ImportValidationErrorDto(row.RowNumber, row.ProductId, $"Brand {row.BrandId} was not found."));

                var missingCategories = row.CategoryIds.Where(id => !existingCategoryIds.Contains(id)).ToList();
                if (missingCategories.Count > 0)
                    errors.Add(new ImportValidationErrorDto(row.RowNumber, row.ProductId,
                        $"Category ID(s) not found: {string.Join(", ", missingCategories)}."));

                if (!string.IsNullOrWhiteSpace(row.Sku) && skuOwners.TryGetValue(row.Sku, out var ownerId))
                {
                    var isSameProduct = row.ProductId.HasValue && row.ProductId.Value == ownerId;
                    if (!isSameProduct)
                        errors.Add(new ImportValidationErrorDto(row.RowNumber, row.ProductId,
                            $"Sku '{row.Sku}' is already used by product {ownerId}."));
                }
            }

            // ZIP is only relevant — and only validated — when the caller actually supplied one.
            ZipImageIndex? zipIndex = null;
            try
            {
                if (zipStream is not null)
                {
                    var zipErrors = new List<string>();
                    zipIndex = _zipSvc.OpenAndIndex(zipStream, zipErrors);
                    foreach (var msg in zipErrors)
                        errors.Add(new ImportValidationErrorDto(0, null, msg));

                    if (zipIndex.IsValidArchive)
                    {
                        foreach (var row in rows)
                        {
                            foreach (var fileName in row.ImageFileNames)
                            {
                                if (!zipIndex.TryGet(fileName, out _))
                                    errors.Add(new ImportValidationErrorDto(row.RowNumber, row.ProductId,
                                        $"Image '{fileName}' referenced in this row was not found in the ZIP archive."));
                            }
                        }
                    }
                }

                if (errors.Count > 0)
                    return new BulkImportResultDto(false, rows.Count, 0, 0, errors);

                return await ApplyImportAsync(rows, zipIndex, ct);
            }
            finally
            {
                zipIndex?.Dispose();
            }
        }
    }

    private async Task<BulkImportResultDto> ApplyImportAsync(List<ProductImportRow> rows, ZipImageIndex? zipIndex, CancellationToken ct)
    {
        // Upload every distinct image referenced across the batch once, even if several rows
        // (or several Image columns on one row) point at the same filename inside the ZIP.
        var uploadedByFileName = new Dictionary<string, (string Url, string StoredFileName)>(StringComparer.OrdinalIgnoreCase);
        var uploadedUrls = new List<string>();

        if (zipIndex is not null)
        {
            var distinctFileNames = rows
                .Where(r => r.ImageFileNames.Count > 0)
                .SelectMany(r => r.ImageFileNames)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var fileName in distinctFileNames)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!zipIndex.TryGet(fileName, out ZipArchiveEntry entry)) continue; // already validated to exist

                    await using var entryStream = entry.Open();

                    await using var memoryStream = new MemoryStream();

                    await entryStream.CopyToAsync(memoryStream, ct);

                    memoryStream.Position = 0;

                    var url = await _storage.UploadAsync(
                        memoryStream,
                        fileName,
                        ContentTypeFor(fileName),
                        "products",
                        ct);

                    uploadedByFileName[fileName] = (url, ExtractStoredFileName(url));
                    uploadedUrls.Add(url);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Bulk product import failed while uploading images to R2; rolling back {Count} already-uploaded images.", uploadedUrls.Count);
                await RollbackUploadedImagesAsync(uploadedUrls);
                return SingleFailure(0, "Failed to upload one or more images to storage. No changes were made.");
            }
        }

        var updateProductIds = rows.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).ToList();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var existingProducts = await _db.Products
                .Include(p => p.ProductCategories)
                .Include(p => p.Images)
                .Where(p => updateProductIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var createdCount = 0;
            var updatedCount = 0;

            foreach (var row in rows)
            {
                Product product;
                if (row.ProductId.HasValue)
                {
                    product = existingProducts[row.ProductId.Value];
                    updatedCount++;
                }
                else
                {
                    // Added (not yet saved) — EF assigns the identity Id and fixes up every child
                    // FK below (ProductCategories/Images) automatically once SaveChangesAsync runs.
                    product = new Product();
                    _db.Products.Add(product);
                    createdCount++;
                }

                product.NameEn = row.NameEn;
                product.NameAr = row.NameAr;
                product.Sku = row.Sku;
                product.DescriptionEn = row.DescriptionEn;
                product.DescriptionAr = row.DescriptionAr;
                if (row.InStock.HasValue) product.InStock = row.InStock.Value;
                if (row.IsVisible.HasValue) product.IsVisible = row.IsVisible.Value;
                product.CostPrice = row.CostPrice;
                product.LowPrice = row.LowPrice;
                product.MediumPrice = row.MediumPrice;
                product.HighPrice = row.HighPrice;
                product.BrandId = row.BrandId!.Value;
                product.UpdatedAt = DateTime.UtcNow;

                product.ProductCategories.Clear();
                foreach (var catId in row.CategoryIds.Distinct())
                    product.ProductCategories.Add(new ProductCategory { CategoryId = catId });

                // Images are only ever touched when a ZIP was supplied AND this row named at least
                // one image — everything else (no ZIP at all, or ZIP with blank Image cells) leaves
                // Product.Images exactly as it was (or, for a new product, simply empty).
                if (zipIndex is not null && row.ImageFileNames.Count > 0)
                {
                    product.Images.Clear();
                    for (var i = 0; i < row.ImageFileNames.Count; i++)
                    {
                        var fileName = row.ImageFileNames[i];
                        if (!uploadedByFileName.TryGetValue(fileName, out var uploaded)) continue;

                        product.Images.Add(new ProductImage
                        {
                            Url = uploaded.Url,
                            OriginalFileName = fileName,
                            StoredFileName = uploaded.StoredFileName,
                            SortOrder = i,
                        });
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Bulk product import completed: {Created} created, {Updated} updated.", createdCount, updatedCount);
            return new BulkImportResultDto(true, rows.Count, createdCount, updatedCount, new List<ImportValidationErrorDto>());
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            await RollbackUploadedImagesAsync(uploadedUrls);
            _logger.LogError(ex, "Bulk product import failed while saving to the database; transaction rolled back and {Count} uploaded images removed.", uploadedUrls.Count);
            return SingleFailure(0, "An unexpected error occurred while saving. No changes were made.");
        }
    }

    private async Task RollbackUploadedImagesAsync(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                // Deliberately CancellationToken.None — cleanup must run even if the request that
                // triggered it was cancelled, otherwise these uploads become permanently orphaned.
                await _storage.DeleteAsync(url, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll back uploaded image {Url} after a failed bulk import.", url);
            }
        }
    }

    private static string ExtractStoredFileName(string url) => url[(url.LastIndexOf('/') + 1)..];

    private static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    private static BulkImportResultDto SingleFailure(int rowNumber, string message) =>
        new(false, 0, 0, 0, new List<ImportValidationErrorDto> { new(rowNumber, null, message) });
}

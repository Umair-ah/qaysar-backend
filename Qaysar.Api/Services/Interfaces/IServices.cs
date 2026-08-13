using Qaysar.Api.DTOs;
using Qaysar.Api.Services;

namespace Qaysar.Api.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}

public interface IBrandService
{
    Task<List<BrandDto>> GetAllAsync();
    Task<BrandDto?> GetByIdAsync(int id);
    Task<BrandDto?> GetBySlugAsync(string slug);
    Task<BrandDto> CreateAsync(BrandUpsertDto dto);
    Task<BrandDto?> UpdateAsync(int id, BrandUpsertDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto?> GetByIdAsync(int id);
    Task<CategoryDto?> GetBySlugAsync(string slug);
    Task<CategoryDto> CreateAsync(CategoryUpsertDto dto);
    Task<CategoryDto?> UpdateAsync(int id, CategoryUpsertDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(
        int page, int pageSize, string? search, int? brandId, int? categoryId, bool onlyVisible, bool? inStock);
    Task<ProductDetailDto?> GetByIdAsync(int id, bool onlyVisible);
    Task<ProductDetailCustomerDto?> GetPublicByIdAsync(int id);
    Task<ProductDetailDto> CreateAsync(ProductUpsertDto dto);
    Task<ProductDetailDto?> UpdateAsync(int id, ProductUpsertDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IQuotationService
{
    Task<QuotationDetailDto> CreateAsync(QuotationCreateDto dto);
    Task<PagedResult<QuotationListItemDto>> GetPagedAsync(int page, int pageSize);
    Task<QuotationDetailDto?> GetByIdAsync(int id);
    Task<QuotationDetailDto?> UpdateStatusAsync(int id, string status);
    Task<QuotationDetailDto?> UpdatePricesAsync(int id, QuotationPricesUpdateDto dto);
}

public interface IZipImageService
{
    /// <summary>
    /// Opens a ZIP stream and indexes its entries by filename (case-insensitive), validating
    /// extensions and rejecting duplicate filenames. Errors are appended to <paramref name="errors"/>
    /// rather than thrown, so batch validation can continue collecting problems.
    /// Caller owns disposal of the returned index (and, transitively, the underlying archive).
    /// </summary>
    ZipImageIndex OpenAndIndex(Stream zipStream, List<string> errors);
}

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder = "uploads", CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}

public interface IProductExportService
{
    /// <summary>Builds an .xlsx workbook of every product (filenames only for images — never URLs).</summary>
    Task<byte[]> ExportAsync(CancellationToken ct = default);
}

public interface IProductImportService
{
    /// <summary>
    /// Bulk-updates existing products from an Excel workbook, optionally replacing their images
    /// from a ZIP of image files. All-or-nothing: if validation fails, nothing is written or uploaded.
    /// </summary>
    Task<BulkImportResultDto> ImportAsync(Stream excelStream, Stream? zipStream, CancellationToken ct = default);
}

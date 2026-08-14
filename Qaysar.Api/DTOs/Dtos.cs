namespace Qaysar.Api.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username);

public record BrandDto(int Id, string NameEn, string NameAr, string Slug, string? ImageUrl);
public record BrandUpsertDto(string NameEn, string NameAr, string? ImageUrl);

public record CategoryDto(int Id, string NameEn, string NameAr, string Slug, string? ImageUrl);
public record CategoryUpsertDto(string NameEn, string NameAr, string? ImageUrl);

public record ProductListItemDto(
    int Id,
    string NameEn,
    string NameAr,
    string Sku,
    string? ImageUrl,
    bool InStock,
    string BrandNameEn,
    string BrandNameAr
);

public record ProductDetailDto(
    int Id,
    string NameEn,
    string NameAr,
    string Sku,
    string DescriptionEn,
    string DescriptionAr,
    List<string> ImageUrls,
    bool InStock,
    bool IsVisible,
    decimal CostPrice,
    decimal LowPrice,
    decimal MediumPrice,
    decimal HighPrice,
    BrandDto Brand,
    List<CategoryDto> Categories
);

// Customer-facing product detail — omits pricing fields (CostPrice/LowPrice/MediumPrice/HighPrice)
// since those are internal-only and must not leak to the storefront API response.
public record ProductDetailCustomerDto(
    int Id,
    string NameEn,
    string NameAr,
    string Sku,
    string DescriptionEn,
    string DescriptionAr,
    List<string> ImageUrls,
    bool InStock,
    bool IsVisible,
    BrandDto Brand,
    List<CategoryDto> Categories
);

public class ProductUpsertDto
{
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string Sku { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public List<string> ImageUrls { get; set; } = new();
    public bool InStock { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public decimal CostPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal MediumPrice { get; set; }
    public decimal HighPrice { get; set; }
    public int BrandId { get; set; }
    public List<int> CategoryIds { get; set; } = new();
}

public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);

public record UploadResponse(string Url);

public record QuotationProductCreateDto(int ProductId, int Quantity);

public class QuotationCreateDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string? AdditionalDetails { get; set; }
    public List<QuotationProductCreateDto> Items { get; set; } = new();
}

public record QuotationListItemDto(int Id, string Name, string Email, string ContactNumber, DateTime CreatedAt, string Status);

public record QuotationItemDto(
    int Id,
    int ProductId,
    string ProductNameEn,
    string ProductNameAr,
    string Sku,
    string? ImageUrl,
    int Quantity,
    decimal CostPrice,
    decimal LowPrice,
    decimal MediumPrice,
    decimal HighPrice,
    decimal? UnitPrice
);

public record QuotationDetailDto(
    int Id,
    string Name,
    string Email,
    string ContactNumber,
    string? AdditionalDetails,
    DateTime CreatedAt,
    string Status,
    List<QuotationItemDto> Items
);

public class QuotationStatusUpdateDto
{
    public string Status { get; set; } = "";
}

public record QuotationItemPriceDto(int Id, decimal? UnitPrice);

public class QuotationPricesUpdateDto
{
    public List<QuotationItemPriceDto> Items { get; set; } = new();
}

// ---- Dashboard ----

public record DashboardRevenuePointDto(DateTime Date, decimal Revenue);
public record DashboardStatusCountDto(string Status, int Count);

public record DashboardStatsDto(
    decimal TotalRevenue,
    decimal RevenueThisMonth,
    decimal RevenueLastMonth,
    decimal RevenueThisWeek,
    int NewQuotationsToday,
    int TotalQuotations,
    int SuccessQuotations,
    int PendingQuotations,
    int FailedQuotations,
    decimal ConversionRatePercent,
    decimal AverageOrderValue,
    List<DashboardRevenuePointDto> RevenueTrend,
    List<DashboardStatusCountDto> StatusBreakdown
);

// ---- Bulk product import/export ----

public record ImportValidationErrorDto(int RowNumber, int? ProductId, string Message);

public record BulkImportResultDto(
    bool Success,
    int TotalRows,
    int CreatedCount,
    int UpdatedCount,
    List<ImportValidationErrorDto> Errors
);

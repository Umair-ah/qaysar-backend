namespace Qaysar.Api.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username);

public record BrandDto(int Id, string NameEn, string NameAr, string Slug);
public record BrandUpsertDto(string NameEn, string NameAr);

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

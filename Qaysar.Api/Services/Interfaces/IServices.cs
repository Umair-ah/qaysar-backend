using Qaysar.Api.DTOs;

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
    Task<ProductDetailDto> CreateAsync(ProductUpsertDto dto);
    Task<ProductDetailDto?> UpdateAsync(int id, ProductUpsertDto dto);
    Task<bool> DeleteAsync(int id);
}

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder = "uploads");
    Task DeleteAsync(string url);
}

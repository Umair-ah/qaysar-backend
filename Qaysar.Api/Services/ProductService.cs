using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(
        int page, int pageSize, string? search, int? brandId, int? categoryId, bool onlyVisible, bool? inStock)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Products.AsNoTracking().Include(p => p.Brand).AsQueryable();

        if (onlyVisible) q = q.Where(p => p.IsVisible);
        if (inStock.HasValue) q = q.Where(p => p.InStock == inStock.Value);
        if (brandId.HasValue) q = q.Where(p => p.BrandId == brandId.Value);
        if (categoryId.HasValue)
            q = q.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId.Value));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                EF.Functions.Like(p.NameEn, $"%{s}%") ||
                EF.Functions.Like(p.NameAr, $"%{s}%") ||
                EF.Functions.Like(p.Sku, $"%{s}%"));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id, p.NameEn, p.NameAr, p.Sku, p.ImageUrl, p.InStock,
                p.Brand!.NameEn, p.Brand!.NameAr))
            .ToListAsync();

        return new PagedResult<ProductListItemDto>(items, total, page, pageSize);
    }

    public async Task<ProductDetailDto?> GetByIdAsync(int id, bool onlyVisible)
    {
        var q = _db.Products.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .AsQueryable();
        if (onlyVisible) q = q.Where(p => p.IsVisible);

        var p2 = await q.FirstOrDefaultAsync(p => p.Id == id);
        return p2 is null ? null : Map(p2);
    }

    public async Task<ProductDetailDto> CreateAsync(ProductUpsertDto dto)
    {
        if (dto.CategoryIds is null || dto.CategoryIds.Count == 0)
            throw new InvalidOperationException("At least one category is required.");
        if (!await _db.Brands.AnyAsync(b => b.Id == dto.BrandId))
            throw new InvalidOperationException("Brand not found.");

        var product = new Product
        {
            NameEn = dto.NameEn,
            NameAr = dto.NameAr,
            Sku = dto.Sku,
            DescriptionEn = dto.DescriptionEn,
            DescriptionAr = dto.DescriptionAr,
            ImageUrl = dto.ImageUrl,
            InStock = dto.InStock,
            IsVisible = dto.IsVisible,
            BrandId = dto.BrandId,
        };

        foreach (var cid in dto.CategoryIds.Distinct())
            product.ProductCategories.Add(new ProductCategory { CategoryId = cid });

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return (await GetByIdAsync(product.Id, false))!;
    }

    public async Task<ProductDetailDto?> UpdateAsync(int id, ProductUpsertDto dto)
    {
        if (dto.CategoryIds is null || dto.CategoryIds.Count == 0)
            throw new InvalidOperationException("At least one category is required.");

        var product = await _db.Products
            .Include(p => p.ProductCategories)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return null;

        product.NameEn = dto.NameEn;
        product.NameAr = dto.NameAr;
        product.Sku = dto.Sku;
        product.DescriptionEn = dto.DescriptionEn;
        product.DescriptionAr = dto.DescriptionAr;
        product.ImageUrl = dto.ImageUrl;
        product.InStock = dto.InStock;
        product.IsVisible = dto.IsVisible;
        product.BrandId = dto.BrandId;
        product.UpdatedAt = DateTime.UtcNow;

        product.ProductCategories.Clear();
        foreach (var cid in dto.CategoryIds.Distinct())
            product.ProductCategories.Add(new ProductCategory { ProductId = id, CategoryId = cid });

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id, false);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return false;
        _db.Products.Remove(p);
        await _db.SaveChangesAsync();
        return true;
    }

    private static ProductDetailDto Map(Product p) => new(
        p.Id, p.NameEn, p.NameAr, p.Sku, p.DescriptionEn, p.DescriptionAr,
        p.ImageUrl, p.InStock, p.IsVisible,
        new BrandDto(p.Brand!.Id, p.Brand.NameEn, p.Brand.NameAr, p.Brand.Slug),
        p.ProductCategories.Select(pc => new CategoryDto(pc.Category!.Id, pc.Category.NameEn, pc.Category.NameAr, pc.Category.Slug, pc.Category.ImageUrl)).ToList()
    );
}

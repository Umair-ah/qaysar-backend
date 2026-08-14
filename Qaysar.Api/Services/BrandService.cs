using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class BrandService : IBrandService
{
    private readonly AppDbContext _db;
    public BrandService(AppDbContext db) => _db = db;

    private static BrandDto ToDto(Brand b) => new(b.Id, b.NameEn, b.NameAr, b.Slug, b.ImageUrl);

    public async Task<List<BrandDto>> GetAllAsync(string? search = null)
    {
        var q = _db.Brands.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b => EF.Functions.Like(b.NameEn, $"%{s}%") || EF.Functions.Like(b.NameAr, $"%{s}%"));
        }
        return await q.OrderBy(b => b.NameEn).Select(b => ToDto(b)).ToListAsync();
    }

    public async Task<BrandDto?> GetByIdAsync(int id)
    {
        var b = await _db.Brands.FindAsync(id);
        return b is null ? null : ToDto(b);
    }

    public async Task<BrandDto?> GetBySlugAsync(string slug)
    {
        var b = await _db.Brands.FirstOrDefaultAsync(x => x.Slug == slug);
        return b is null ? null : ToDto(b);
    }

    public async Task<BrandDto> CreateAsync(BrandUpsertDto dto)
    {
        var slug = await UniqueSlug(SlugHelper.Slugify(dto.NameEn));
        var brand = new Brand { NameEn = dto.NameEn, NameAr = dto.NameAr, Slug = slug, ImageUrl = dto.ImageUrl };
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync();
        return ToDto(brand);
    }

    public async Task<BrandDto?> UpdateAsync(int id, BrandUpsertDto dto)
    {
        var b = await _db.Brands.FindAsync(id);
        if (b is null) return null;
        b.NameEn = dto.NameEn;
        b.NameAr = dto.NameAr;
        b.ImageUrl = dto.ImageUrl;
        await _db.SaveChangesAsync();
        return ToDto(b);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var b = await _db.Brands.FindAsync(id);
        if (b is null) return false;
        _db.Brands.Remove(b);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> UniqueSlug(string baseSlug)
    {
        var slug = baseSlug;
        var i = 1;
        while (await _db.Brands.AnyAsync(b => b.Slug == slug))
        {
            slug = $"{baseSlug}-{i++}";
        }
        return slug;
    }
}

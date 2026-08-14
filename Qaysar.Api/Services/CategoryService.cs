using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    public CategoryService(AppDbContext db) => _db = db;

    private static CategoryDto ToDto(Category c) => new(c.Id, c.NameEn, c.NameAr, c.Slug, c.ImageUrl);

    public async Task<List<CategoryDto>> GetAllAsync(string? search = null)
    {
        var q = _db.Categories.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => EF.Functions.Like(c.NameEn, $"%{s}%") || EF.Functions.Like(c.NameAr, $"%{s}%"));
        }
        return await q.OrderBy(c => c.NameEn).Select(c => ToDto(c)).ToListAsync();
    }

    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        return c is null ? null : ToDto(c);
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug)
    {
        var c = await _db.Categories.FirstOrDefaultAsync(x => x.Slug == slug);
        return c is null ? null : ToDto(c);
    }

    public async Task<CategoryDto> CreateAsync(CategoryUpsertDto dto)
    {
        var slug = await UniqueSlug(SlugHelper.Slugify(dto.NameEn));
        var c = new Category { NameEn = dto.NameEn, NameAr = dto.NameAr, Slug = slug, ImageUrl = dto.ImageUrl };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<CategoryDto?> UpdateAsync(int id, CategoryUpsertDto dto)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return null;
        c.NameEn = dto.NameEn;
        c.NameAr = dto.NameAr;
        c.ImageUrl = dto.ImageUrl;
        await _db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null) return false;
        _db.Categories.Remove(c);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> UniqueSlug(string baseSlug)
    {
        var slug = baseSlug;
        var i = 1;
        while (await _db.Categories.AnyAsync(c => c.Slug == slug))
        {
            slug = $"{baseSlug}-{i++}";
        }
        return slug;
    }
}

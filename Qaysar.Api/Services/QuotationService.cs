using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class QuotationService : IQuotationService
{
    private readonly AppDbContext _db;
    public QuotationService(AppDbContext db) => _db = db;

    public async Task<QuotationDetailDto> CreateAsync(QuotationCreateDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            throw new InvalidOperationException("At least one product is required.");
        if (dto.Items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Quantity must be greater than zero.");

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var foundCount = await _db.Products.CountAsync(p => productIds.Contains(p.Id));
        if (foundCount != productIds.Count)
            throw new InvalidOperationException("One or more products were not found.");

        var quotation = new Quotation
        {
            Email = dto.Email,
            ContactNumber = dto.ContactNumber,
            AdditionalDetails = string.IsNullOrWhiteSpace(dto.AdditionalDetails) ? null : dto.AdditionalDetails.Trim(),
            Status = QuotationStatus.New,
        };

        foreach (var item in dto.Items)
            quotation.QuotationProducts.Add(new QuotationProduct { ProductId = item.ProductId, Quantity = item.Quantity });

        _db.Quotations.Add(quotation);
        await _db.SaveChangesAsync();

        return (await GetByIdAsync(quotation.Id))!;
    }

    public async Task<PagedResult<QuotationListItemDto>> GetPagedAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Quotations.AsNoTracking().OrderByDescending(x => x.CreatedAt);

        var total = await q.CountAsync();
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new QuotationListItemDto(x.Id, x.Email, x.ContactNumber, x.CreatedAt, x.Status.ToString()))
            .ToListAsync();

        return new PagedResult<QuotationListItemDto>(items, total, page, pageSize);
    }

    public async Task<QuotationDetailDto?> GetByIdAsync(int id)
    {
        var q = await _db.Quotations.AsNoTracking()
            .Include(x => x.QuotationProducts).ThenInclude(qp => qp.Product).ThenInclude(p => p!.Images)
            .FirstOrDefaultAsync(x => x.Id == id);

        return q is null ? null : Map(q);
    }

    public async Task<QuotationDetailDto?> UpdateStatusAsync(int id, string status)
    {
        if (!Enum.TryParse<QuotationStatus>(status, out var parsed))
            throw new InvalidOperationException("Invalid status.");

        var q = await _db.Quotations.FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return null;

        q.Status = parsed;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<QuotationDetailDto?> UpdatePricesAsync(int id, QuotationPricesUpdateDto dto)
    {
        var quotation = await _db.Quotations
            .Include(x => x.QuotationProducts)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (quotation is null) return null;

        var itemsById = quotation.QuotationProducts.ToDictionary(qp => qp.Id);
        foreach (var item in dto.Items ?? new())
        {
            if (itemsById.TryGetValue(item.Id, out var qp))
                qp.UnitPrice = item.UnitPrice;
        }

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    private static QuotationDetailDto Map(Quotation q) => new(
        q.Id, q.Email, q.ContactNumber, q.AdditionalDetails, q.CreatedAt, q.Status.ToString(),
        q.QuotationProducts.Select(qp => new QuotationItemDto(
            qp.Id,
            qp.ProductId,
            qp.Product!.NameEn,
            qp.Product.NameAr,
            qp.Product.Sku,
            qp.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            qp.Quantity,
            qp.Product.CostPrice,
            qp.Product.LowPrice,
            qp.Product.MediumPrice,
            qp.Product.HighPrice,
            qp.UnitPrice
        )).ToList()
    );
}

using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.DTOs;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;

namespace Qaysar.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        // Quotation.CreatedAt is stamped with DateTime.UtcNow, so every boundary below is UTC.
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var firstOfThisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var startOfWeek = today.AddDays(-daysSinceMonday);

        // Revenue is the VAT-inclusive line total (UnitPrice * Quantity) of items on Success quotations —
        // the same figure the quotation PDF's grand total is built from.
        var revenueLines =
            from qp in _db.QuotationProducts
            join quotation in _db.Quotations on qp.QuotationId equals quotation.Id
            where quotation.Status == QuotationStatus.Success
            select new { quotation.CreatedAt, Amount = (qp.UnitPrice ?? 0m) * qp.Quantity };

        var totalRevenue = await revenueLines.SumAsync(x => x.Amount);
        var revenueThisMonth = await revenueLines.Where(x => x.CreatedAt >= firstOfThisMonth).SumAsync(x => x.Amount);
        var revenueLastMonth = await revenueLines
            .Where(x => x.CreatedAt >= firstOfLastMonth && x.CreatedAt < firstOfThisMonth)
            .SumAsync(x => x.Amount);
        var revenueThisWeek = await revenueLines.Where(x => x.CreatedAt >= startOfWeek).SumAsync(x => x.Amount);

        var newToday = await _db.Quotations.CountAsync(q => q.Status == QuotationStatus.New && q.CreatedAt >= today && q.CreatedAt < tomorrow);
        var totalQuotations = await _db.Quotations.CountAsync();
        var successCount = await _db.Quotations.CountAsync(q => q.Status == QuotationStatus.Success);
        var pendingCount = await _db.Quotations.CountAsync(q => q.Status == QuotationStatus.New || q.Status == QuotationStatus.InProgress);
        var failedCount = await _db.Quotations.CountAsync(q => q.Status == QuotationStatus.Failed);

        var conversionRate = totalQuotations == 0 ? 0m : Math.Round(successCount * 100m / totalQuotations, 1);
        var avgOrderValue = successCount == 0 ? 0m : Math.Round(totalRevenue / successCount, 2);

        // 14-day daily revenue trend, zero-filled for days with no Success revenue.
        var trendStart = today.AddDays(-13);
        var trendGrouped = await revenueLines
            .Where(x => x.CreatedAt >= trendStart)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.Amount) })
            .ToListAsync();
        var trendMap = trendGrouped.ToDictionary(x => x.Date, x => x.Revenue);
        var revenueTrend = new List<DashboardRevenuePointDto>();
        for (var d = trendStart; d <= today; d = d.AddDays(1))
            revenueTrend.Add(new DashboardRevenuePointDto(d, trendMap.TryGetValue(d, out var amt) ? amt : 0m));

        var statusCounts = await _db.Quotations
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var statusMap = statusCounts.ToDictionary(x => x.Status, x => x.Count);
        var statusBreakdown = Enum.GetValues<QuotationStatus>()
            .Select(s => new DashboardStatusCountDto(s.ToString(), statusMap.TryGetValue(s, out var c) ? c : 0))
            .ToList();

        return new DashboardStatsDto(
            TotalRevenue: totalRevenue,
            RevenueThisMonth: revenueThisMonth,
            RevenueLastMonth: revenueLastMonth,
            RevenueThisWeek: revenueThisWeek,
            NewQuotationsToday: newToday,
            TotalQuotations: totalQuotations,
            SuccessQuotations: successCount,
            PendingQuotations: pendingCount,
            FailedQuotations: failedCount,
            ConversionRatePercent: conversionRate,
            AverageOrderValue: avgOrderValue,
            RevenueTrend: revenueTrend,
            StatusBreakdown: statusBreakdown
        );
    }
}

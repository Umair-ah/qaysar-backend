using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Data;
using Qaysar.Api.Models;
using Qaysar.Api.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Qaysar.Api.Services;

public class QuotationPdfService : IQuotationPdfService
{
    private readonly AppDbContext _db;
    public QuotationPdfService(AppDbContext db) => _db = db;

    private const string CompanyName = "Qaysar Al-Muaddat";
    private const string CompanyAddressLine1 = "Prince Abdulaziz bin Musaid bin Jalawi St (Al Dabab St), Al Murabba";
    private const string CompanyAddressLine2 = "Building No. 6049, intersection with Al Washm St, Riyadh 12628, Saudi Arabia";
    private const string CompanyPhones = "+966 579 6989 18  ·  +966 579 6989 37";
    private const string CompanyEmail = "qaysaralmuaddat@gmail.com";
    private const string Currency = "SAR";
    private const int ValidityDays = 30;
    private const decimal VatRate = 0.15m;

    private const string ColorPlum = "#5e0a0b";
    private const string ColorSand = "#987756";
    private const string ColorMist = "#d9cbba";
    private const string ColorCream = "#faf6f2";
    private const string ColorInk = "#1a1113";
    private const string ColorWhite = "#FFFFFF";

    private static readonly string LogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "qaysar-logo.png");

    public async Task<byte[]?> GenerateAsync(int id, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations.AsNoTracking()
            .Include(q => q.QuotationProducts).ThenInclude(qp => qp.Product)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quotation is null) return null;

        var items = quotation.QuotationProducts.OrderBy(qp => qp.Id).ToList();
        // Product unit prices are already VAT-inclusive, so back the base price and VAT amount out of the total.
        var grandTotal = items.Sum(qp => (qp.UnitPrice ?? 0m) * qp.Quantity);
        var subtotalExclVat = grandTotal / (1 + VatRate);
        var vatAmount = grandTotal - subtotalExclVat;
        var hasPendingPrices = items.Any(qp => qp.UnitPrice is null);
        var logoBytes = File.Exists(LogoPath) ? await File.ReadAllBytesAsync(LogoPath, ct) : null;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(ColorInk));

                page.Header().Element(c => ComposeHeader(c, logoBytes, quotation));
                page.Content().Element(c => ComposeContent(c, quotation, items, subtotalExclVat, vatAmount, grandTotal, hasPendingPrices));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, byte[]? logoBytes, Quotation q)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(150).Height(56).AlignLeft().AlignMiddle().Element(c =>
                {
                    if (logoBytes is not null) c.Image(logoBytes).FitArea();
                    else c.Text(CompanyName).FontSize(18).Bold().FontColor(ColorPlum);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(CompanyName).FontSize(13).Bold().FontColor(ColorPlum);
                    col.Item().Text(CompanyAddressLine1).FontSize(8).FontColor(ColorSand);
                    col.Item().Text(CompanyAddressLine2).FontSize(8).FontColor(ColorSand);
                    col.Item().Text(CompanyPhones).FontSize(8).FontColor(ColorSand);
                    col.Item().Text(CompanyEmail).FontSize(8).FontColor(ColorSand);
                });
            });

            column.Item().PaddingTop(12).LineHorizontal(1).LineColor(ColorMist);

            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("QUOTATION").FontSize(20).Bold().FontColor(ColorPlum);
                    col.Item().Text($"#{q.Id:D5}").FontSize(11).FontColor(ColorSand);
                });

                row.ConstantItem(180).Column(col =>
                {
                    col.Item().AlignRight().Text("Date Issued").FontSize(8).FontColor(ColorSand);
                    col.Item().AlignRight().Text(q.CreatedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(10).SemiBold();
                    col.Item().PaddingTop(6).AlignRight().Text("Valid Until").FontSize(8).FontColor(ColorSand);
                    col.Item().AlignRight().Text(q.CreatedAt.AddDays(ValidityDays).ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(10).SemiBold();
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, Quotation q, List<QuotationProduct> items, decimal subtotalExclVat, decimal vatAmount, decimal grandTotal, bool hasPendingPrices)
    {
        container.PaddingTop(18).Column(column =>
        {
            column.Spacing(14);

            column.Item().Element(c => ComposeCustomer(c, q));
            column.Item().Element(c => ComposeTable(c, items));
            column.Item().Text("Unit and line prices above include 15% VAT.").FontSize(8).Italic().FontColor(ColorSand);
            column.Item().AlignRight().Width(240).Element(c => ComposeTotal(c, subtotalExclVat, vatAmount, grandTotal));

            if (hasPendingPrices)
                column.Item().Text("* One or more items are still pending final pricing and are shown as SAR 0.00 above.")
                    .FontSize(8).Italic().FontColor(ColorSand);

            if (!string.IsNullOrWhiteSpace(q.AdditionalDetails))
            {
                column.Item().Column(col =>
                {
                    col.Item().Text("Additional Details").FontSize(10).Bold().FontColor(ColorPlum);
                    col.Item().PaddingTop(2).Text(q.AdditionalDetails).FontSize(9).FontColor(ColorInk);
                });
            }

            column.Item().PaddingTop(6).Text($"This quotation is valid for {ValidityDays} days from the date of issue. All amounts are in Saudi Riyal (SAR) and include 15% Value Added Tax (VAT) in accordance with ZATCA regulations.")
                .FontSize(8).FontColor(ColorSand);
        });
    }

    private static void ComposeCustomer(IContainer container, Quotation q)
    {
        container.Background(ColorCream).Padding(12).Column(col =>
        {
            col.Item().Text("Prepared For").FontSize(9).Bold().FontColor(ColorSand);
            col.Item().PaddingTop(2).Text(q.Email).FontSize(10).FontColor(ColorInk);
            col.Item().Text(q.ContactNumber).FontSize(10).FontColor(ColorInk);
        });
    }

    private static void ComposeTable(IContainer container, List<QuotationProduct> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(3);
                columns.RelativeColumn(1.3f);
                columns.ConstantColumn(40);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(1.2f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Product");
                header.Cell().Element(HeaderCell).Text("SKU");
                header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                header.Cell().Element(HeaderCell).AlignRight().Text("Unit Price");
                header.Cell().Element(HeaderCell).AlignRight().Text("Total");

                static IContainer HeaderCell(IContainer c) => c
                    .Background(ColorPlum)
                    .PaddingVertical(8).PaddingHorizontal(6)
                    .DefaultTextStyle(x => x.FontColor(ColorWhite).Bold().FontSize(9));
            });

            var index = 1;
            foreach (var item in items)
            {
                var bg = index % 2 == 0 ? ColorCream : ColorWhite;
                var lineTotal = (item.UnitPrice ?? 0m) * item.Quantity;

                table.Cell().Element(c => BodyCell(c, bg)).Text(index.ToString());
                table.Cell().Element(c => BodyCell(c, bg)).Text(item.Product!.NameEn);
                table.Cell().Element(c => BodyCell(c, bg)).Text(item.Product!.Sku).FontColor(ColorSand);
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight().Text(item.Quantity.ToString());
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight()
                    .Text(item.UnitPrice is null ? "—" : FormatMoney(item.UnitPrice.Value));
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight().Text(FormatMoney(lineTotal)).Bold();

                index++;
            }

            static IContainer BodyCell(IContainer c, string bg) => c
                .Background(bg)
                .BorderBottom(1).BorderColor(ColorMist)
                .PaddingVertical(7).PaddingHorizontal(6)
                .DefaultTextStyle(x => x.FontSize(9));
        });
    }

    private static void ComposeTotal(IContainer container, decimal subtotalExclVat, decimal vatAmount, decimal grandTotal)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Subtotal (excl. VAT)").FontSize(9).FontColor(ColorSand);
                row.AutoItem().Text(FormatMoney(subtotalExclVat)).FontSize(9).FontColor(ColorInk);
            });
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text("VAT (15%)").FontSize(9).FontColor(ColorSand);
                row.AutoItem().Text(FormatMoney(vatAmount)).FontSize(9).FontColor(ColorInk);
            });
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(ColorMist);
            column.Item().PaddingTop(8).Background(ColorPlum).Padding(12).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text("Grand Total (incl. VAT)").FontColor(ColorWhite).FontSize(11).Bold();
                row.AutoItem().Text(FormatMoney(grandTotal)).FontColor(ColorWhite).FontSize(15).Bold();
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(ColorMist);
            column.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text($"{CompanyName}  ·  {CompanyEmail}  ·  {CompanyPhones}").FontSize(7).FontColor(ColorSand);
                row.AutoItem().Text(x =>
                {
                    x.Span("Page ").FontSize(7).FontColor(ColorSand);
                    x.CurrentPageNumber().FontSize(7).FontColor(ColorSand);
                    x.Span(" / ").FontSize(7).FontColor(ColorSand);
                    x.TotalPages().FontSize(7).FontColor(ColorSand);
                });
            });
        });
    }

    private static string FormatMoney(decimal value) => $"{Currency} {value.ToString("N2", CultureInfo.InvariantCulture)}";
}

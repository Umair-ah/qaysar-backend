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
    private const string CompanyCr = "7052774036";
    private const string CompanyVat = "314448086500003";
    private const string CompanyIban = "SA9345000000602083602001";
    private const string BankNameEn = "Saudi Awwal Bank (SAB)";
    private const string BankNameAr = "البنك السعودي الأول";
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
    // Drop a "sab-logo.png" into Assets/ to have the bank's logo render here — falls back to a text badge until then.
    private static readonly string SabLogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "sab-logo.png");

    public async Task<byte[]?> GenerateAsync(int id, CancellationToken ct = default)
    {
        var quotation = await _db.Quotations.AsNoTracking()
            .Include(q => q.QuotationProducts).ThenInclude(qp => qp.Product)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
        if (quotation is null) return null;

        var items = quotation.QuotationProducts.OrderBy(qp => qp.Id).ToList();
        // Line total is the VAT-inclusive selling price; split it into an 85% net (price) portion and a 15% VAT portion.
        var lines = items.Select(qp =>
        {
            var lineTotal = (qp.UnitPrice ?? 0m) * qp.Quantity;
            var linePrice = Math.Round(lineTotal * (1 - VatRate), 2);
            var lineVat = lineTotal - linePrice;
            var unitPriceExclVat = qp.UnitPrice is null ? (decimal?)null : Math.Round(qp.UnitPrice.Value * (1 - VatRate), 2);
            return new QuotationLine(qp, unitPriceExclVat, linePrice, lineVat, lineTotal);
        }).ToList();

        var subtotalExclVat = lines.Sum(l => l.Price);
        var vatAmount = lines.Sum(l => l.Vat);
        var grandTotal = lines.Sum(l => l.Total);

        if (subtotalExclVat == 0 && vatAmount == 0 && grandTotal == 0)
            throw new InvalidOperationException("Please set prices for the quotation items before generating the PDF.");

        var hasPendingPrices = items.Any(qp => qp.UnitPrice is null);
        var logoBytes = File.Exists(LogoPath) ? await File.ReadAllBytesAsync(LogoPath, ct) : null;
        var sabLogoBytes = File.Exists(SabLogoPath) ? await File.ReadAllBytesAsync(SabLogoPath, ct) : null;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(ColorInk));

                page.Header().Element(c => ComposeHeader(c, logoBytes, quotation));
                page.Content().Element(c => ComposeContent(c, quotation, lines, subtotalExclVat, vatAmount, grandTotal, hasPendingPrices, sabLogoBytes));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private record QuotationLine(QuotationProduct Item, decimal? UnitPriceExclVat, decimal Price, decimal Vat, decimal Total);

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
                    col.Item().Text(text =>
                    {
                        text.Span("QUOTATION").FontSize(20).Bold().FontColor(ColorPlum);
                        text.Span("  /  ").FontSize(14).FontColor(ColorMist);
                        text.Span("عرض سعر").FontSize(14).Bold().FontColor(ColorPlum);
                    });
                    col.Item().Text($"#{q.Id:D5}").FontSize(11).FontColor(ColorSand);
                });

                row.ConstantItem(180).Column(col =>
                {
                    col.Item().AlignRight().Text(text =>
                    {
                        text.Span("Date Issued / ").FontSize(8).FontColor(ColorSand);
                        text.Span("تاريخ الإصدار").FontSize(8).FontColor(ColorSand);
                    });
                    col.Item().AlignRight().Text(q.CreatedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(10).SemiBold();
                    col.Item().PaddingTop(6).AlignRight().Text(text =>
                    {
                        text.Span("Valid Until / ").FontSize(8).FontColor(ColorSand);
                        text.Span("صالح حتى").FontSize(8).FontColor(ColorSand);
                    });
                    col.Item().AlignRight().Text(q.CreatedAt.AddDays(ValidityDays).ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(10).SemiBold();
                });
            });
        });
    }

    private static void ComposeContent(IContainer container, Quotation q, List<QuotationLine> lines, decimal subtotalExclVat, decimal vatAmount, decimal grandTotal, bool hasPendingPrices, byte[]? sabLogoBytes)
    {
        container.PaddingTop(18).Column(column =>
        {
            column.Spacing(14);

            column.Item().Element(ComposeCompanyLegal);
            column.Item().Element(c => ComposeCustomer(c, q));
            column.Item().Element(c => ComposeTable(c, lines));
            column.Item().Text(text =>
            {
                text.Span("Price excludes VAT; VAT (15%) is shown separately per line. / ").FontSize(8).Italic().FontColor(ColorSand);
                text.Span("السعر لا يشمل ضريبة القيمة المضافة؛ يتم عرض الضريبة (15٪) بشكل منفصل لكل بند.").FontSize(8).Italic().FontColor(ColorSand);
            });
            column.Item().AlignRight().Width(260).Element(c => ComposeTotal(c, subtotalExclVat, vatAmount, grandTotal));

            if (hasPendingPrices)
                column.Item().Text(text =>
                {
                    text.Span("* One or more items are still pending final pricing and are shown as SAR 0.00 above. / ").FontSize(8).Italic().FontColor(ColorSand);
                    text.Span("بعض البنود لا تزال قيد التسعير النهائي وتظهر أعلاه بقيمة 0.00 ريال سعودي.").FontSize(8).Italic().FontColor(ColorSand);
                });

            if (!string.IsNullOrWhiteSpace(q.AdditionalDetails))
            {
                column.Item().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Additional Details / ").FontSize(10).Bold().FontColor(ColorPlum);
                        text.Span("تفاصيل إضافية").FontSize(10).Bold().FontColor(ColorPlum);
                    });
                    col.Item().PaddingTop(2).Text(q.AdditionalDetails).FontSize(9).FontColor(ColorInk);
                });
            }

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span($"This quotation is valid for {ValidityDays} days from the date of issue. All amounts are in Saudi Riyal (SAR) and include 15% Value Added Tax (VAT) in accordance with ZATCA regulations. / ")
                    .FontSize(8).FontColor(ColorSand);
                text.Span($"هذا العرض صالح لمدة {ValidityDays} يومًا من تاريخ الإصدار. جميع المبالغ بالريال السعودي وتشمل ضريبة القيمة المضافة (15٪) وفقًا لأنظمة هيئة الزكاة والضريبة والجمارك.")
                    .FontSize(8).FontColor(ColorSand);
            });

            column.Item().Element(c => ComposeBankDetails(c, sabLogoBytes));
        });
    }

    private static void ComposeBankDetails(IContainer container, byte[]? sabLogoBytes)
    {
        container.Background(ColorCream).Padding(12).Row(row =>
        {
            row.ConstantItem(48).Height(32).AlignLeft().AlignMiddle().Element(c =>
            {
                if (sabLogoBytes is not null) c.Image(sabLogoBytes).FitArea();
                else c.Background(ColorPlum).AlignMiddle().AlignCenter().Padding(4).Text("SAB").FontSize(11).Bold().FontColor(ColorWhite);
            });

            row.RelativeItem().PaddingLeft(12).Column(col =>
            {
                col.Item().Text(text =>
                {
                    text.Span("Bank Transfer / ").FontSize(9).Bold().FontColor(ColorSand);
                    text.Span("تحويل بنكي").FontSize(9).Bold().FontColor(ColorSand);
                });
                col.Item().PaddingTop(2).Text(text =>
                {
                    text.Span($"{BankNameEn} / {BankNameAr}").FontSize(9).FontColor(ColorInk);
                });
                col.Item().PaddingTop(2).Text(text =>
                {
                    text.Span("IBAN: ").FontSize(10).Bold().FontColor(ColorPlum);
                    text.Span(CompanyIban).FontSize(10).Bold().FontColor(ColorPlum);
                });
            });
        });
    }

    private static void ComposeCompanyLegal(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("CR: ").FontSize(8).SemiBold().FontColor(ColorSand);
                text.Span(CompanyCr).FontSize(8).FontColor(ColorInk);
                text.Span("     ").FontSize(8);
                text.Span("VAT Number: ").FontSize(8).SemiBold().FontColor(ColorSand);
                text.Span(CompanyVat).FontSize(8).FontColor(ColorInk);
            });
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("السجل التجاري: ").FontSize(8).SemiBold().FontColor(ColorSand);
                text.Span(CompanyCr).FontSize(8).FontColor(ColorInk);
                text.Span("     ").FontSize(8);
                text.Span("الرقم الضريبي: ").FontSize(8).SemiBold().FontColor(ColorSand);
                text.Span(CompanyVat).FontSize(8).FontColor(ColorInk);
            });
        });
    }

    private static void ComposeCustomer(IContainer container, Quotation q)
    {
        container.Background(ColorCream).Padding(12).Column(col =>
        {
            col.Item().Text(text =>
            {
                text.Span("Prepared For / ").FontSize(9).Bold().FontColor(ColorSand);
                text.Span("إعداد لـ").FontSize(9).Bold().FontColor(ColorSand);
            });
            col.Item().PaddingTop(2).Text(q.Name).FontSize(11).Bold().FontColor(ColorPlum);
            col.Item().Text(q.Email).FontSize(10).FontColor(ColorInk);
            col.Item().Text(q.ContactNumber).FontSize(10).FontColor(ColorInk);
        });
    }

    private static void ComposeTable(IContainer container, List<QuotationLine> lines)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.05f);
                columns.RelativeColumn(1.1f);
                columns.ConstantColumn(30);
                columns.RelativeColumn(1.05f);
                columns.RelativeColumn(1.0f);
                columns.RelativeColumn(1.05f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text(text => Bilingual(text, "#", "#"));
                header.Cell().Element(HeaderCell).Text(text => Bilingual(text, "Product", "المنتج"));
                header.Cell().Element(HeaderCell).Text(text => Bilingual(text, "SKU", "رمز المنتج"));
                header.Cell().Element(HeaderCell).AlignRight().Text(text => Bilingual(text, "Unit Price", "سعر الوحدة"));
                header.Cell().Element(HeaderCell).AlignRight().Text(text => Bilingual(text, "Qty", "الكمية"));
                header.Cell().Element(HeaderCell).AlignRight().Text(text => Bilingual(text, "Price", "السعر"));
                header.Cell().Element(HeaderCell).AlignRight().Text(text => Bilingual(text, "VAT", "الضريبة"));
                header.Cell().Element(HeaderCell).AlignRight().Text(text => Bilingual(text, "Total", "الإجمالي"));

                static IContainer HeaderCell(IContainer c) => c
                    .Background(ColorPlum)
                    .PaddingVertical(8).PaddingHorizontal(6)
                    .DefaultTextStyle(x => x.FontColor(ColorWhite).Bold().FontSize(8.5f));

                static void Bilingual(TextDescriptor text, string en, string ar)
                {
                    text.Line(en);
                    text.Line(ar).FontSize(7).Light();
                }
            });

            var index = 1;
            foreach (var line in lines)
            {
                var item = line.Item;
                var bg = index % 2 == 0 ? ColorCream : ColorWhite;

                table.Cell().Element(c => BodyCell(c, bg)).Text(index.ToString());
                table.Cell().Element(c => BodyCell(c, bg)).Text(text =>
                {
                    text.Line(item.Product!.NameEn);
                    if (!string.IsNullOrWhiteSpace(item.Product!.NameAr))
                        text.Line(item.Product!.NameAr).FontSize(7).FontColor(ColorSand);
                });
                table.Cell().Element(c => BodyCell(c, bg)).Text(item.Product!.Sku).FontColor(ColorSand);
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight()
                    .Text(line.UnitPriceExclVat is null ? "—" : FormatMoney(line.UnitPriceExclVat.Value));
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight().Text(item.Quantity.ToString());
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight()
                    .Text(item.UnitPrice is null ? "—" : FormatMoney(line.Price));
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight()
                    .Text(item.UnitPrice is null ? "—" : FormatMoney(line.Vat));
                table.Cell().Element(c => BodyCell(c, bg)).AlignRight().Text(FormatMoney(line.Total)).Bold();

                index++;
            }

            static IContainer BodyCell(IContainer c, string bg) => c
                .Background(bg)
                .BorderBottom(1).BorderColor(ColorMist)
                .PaddingVertical(6).PaddingHorizontal(6)
                .DefaultTextStyle(x => x.FontSize(8));
        });
    }

    private static void ComposeTotal(IContainer container, decimal subtotalExclVat, decimal vatAmount, decimal grandTotal)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Line("Net Amount").FontSize(9).FontColor(ColorSand);
                    text.Line("صافي المبلغ").FontSize(8).FontColor(ColorSand);
                });
                row.AutoItem().AlignMiddle().Text(FormatMoney(subtotalExclVat)).FontSize(9).FontColor(ColorInk);
            });
            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Line("VAT (15%)").FontSize(9).FontColor(ColorSand);
                    text.Line("ضريبة القيمة المضافة (15٪)").FontSize(8).FontColor(ColorSand);
                });
                row.AutoItem().AlignMiddle().Text(FormatMoney(vatAmount)).FontSize(9).FontColor(ColorInk);
            });
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(ColorMist);
            column.Item().PaddingTop(8).Background(ColorPlum).Padding(12).Row(row =>
            {
                row.RelativeItem().AlignMiddle().Text(text =>
                {
                    text.Line("Total").FontColor(ColorWhite).FontSize(11).Bold();
                    text.Line("الإجمالي").FontColor(ColorWhite).FontSize(9).Bold();
                });
                row.AutoItem().AlignMiddle().Text(FormatMoney(grandTotal)).FontColor(ColorWhite).FontSize(15).Bold();
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
                row.RelativeItem().Text(text =>
                {
                    text.Span($"{CompanyName}  ·  {CompanyEmail}  ·  {CompanyPhones}").FontSize(7).FontColor(ColorSand);
                    text.Span($"   ·   CR {CompanyCr}   ·   VAT {CompanyVat}").FontSize(7).FontColor(ColorSand);
                });
                row.AutoItem().Text(x =>
                {
                    x.Span("Page / صفحة ").FontSize(7).FontColor(ColorSand);
                    x.CurrentPageNumber().FontSize(7).FontColor(ColorSand);
                    x.Span(" / ").FontSize(7).FontColor(ColorSand);
                    x.TotalPages().FontSize(7).FontColor(ColorSand);
                });
            });
        });
    }

    private static string FormatMoney(decimal value) => $"{Currency} {value.ToString("N2", CultureInfo.InvariantCulture)}";
}

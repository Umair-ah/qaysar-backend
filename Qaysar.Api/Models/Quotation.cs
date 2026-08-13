namespace Qaysar.Api.Models;

public enum QuotationStatus
{
    New,
    InProgress,
    Failed,
    Success
}

public class Quotation
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string ContactNumber { get; set; } = "";
    public string? AdditionalDetails { get; set; }
    public QuotationStatus Status { get; set; } = QuotationStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<QuotationProduct> QuotationProducts { get; set; } = new List<QuotationProduct>();
}

public class QuotationProduct
{
    public int Id { get; set; }
    public int QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}

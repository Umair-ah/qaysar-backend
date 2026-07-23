namespace Qaysar.Api.Models;

public class Product
{
    public int Id { get; set; }
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string Sku { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public string? ImageUrl { get; set; }
    public bool InStock { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public int BrandId { get; set; }
    public Brand? Brand { get; set; }

    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductCategory
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

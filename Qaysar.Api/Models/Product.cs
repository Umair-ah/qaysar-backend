namespace Qaysar.Api.Models;

public class Product
{
    public int Id { get; set; }
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string Sku { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public string DescriptionAr { get; set; } = "";
    public bool InStock { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    public int BrandId { get; set; }
    public Brand? Brand { get; set; }

    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string Url { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ProductCategory
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

using Microsoft.EntityFrameworkCore;
using Qaysar.Api.Models;

namespace Qaysar.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationProduct> QuotationProducts => Set<QuotationProduct>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Brand>(e =>
        {
            e.Property(b => b.NameEn).HasMaxLength(200).IsRequired();
            e.Property(b => b.NameAr).HasMaxLength(200).IsRequired();
            e.Property(b => b.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(b => b.Slug).IsUnique();
        });

        mb.Entity<Category>(e =>
        {
            e.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
            e.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(c => c.Slug).IsUnique();
        });

        mb.Entity<Product>(e =>
        {
            e.Property(p => p.NameEn).HasMaxLength(300).IsRequired();
            e.Property(p => p.NameAr).HasMaxLength(300).IsRequired();
            e.Property(p => p.Sku).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.Sku).IsUnique();
            e.HasIndex(p => p.IsVisible);
            e.HasIndex(p => p.BrandId);
            e.HasOne(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<ProductCategory>(e =>
        {
            e.HasKey(pc => new { pc.ProductId, pc.CategoryId });
            e.HasOne(pc => pc.Product)
                .WithMany(p => p.ProductCategories)
                .HasForeignKey(pc => pc.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pc => pc.Category)
                .WithMany(c => c.ProductCategories)
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ProductImage>(e =>
        {
            e.Property(pi => pi.Url).HasMaxLength(1000).IsRequired();
            e.HasIndex(pi => new { pi.ProductId, pi.SortOrder });
            e.HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<AdminUser>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
        });

        mb.Entity<Quotation>(e =>
        {
            e.Property(q => q.Email).HasMaxLength(300).IsRequired();
            e.Property(q => q.ContactNumber).HasMaxLength(50).IsRequired();
            e.Property(q => q.AdditionalDetails).HasMaxLength(2000);
            e.Property(q => q.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(q => q.CreatedAt);
        });

        mb.Entity<QuotationProduct>(e =>
        {
            e.HasOne(qp => qp.Quotation)
                .WithMany(q => q.QuotationProducts)
                .HasForeignKey(qp => qp.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(qp => qp.Product)
                .WithMany()
                .HasForeignKey(qp => qp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

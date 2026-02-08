using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ProductListingConfiguration : IEntityTypeConfiguration<ProductListing>
{
    public void Configure(EntityTypeBuilder<ProductListing> builder)
    {
        builder.ToTable("product_listing");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.ProductInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.StatusInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.SeoInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.Images).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.Branch).WithMany(b => b.ProductListings).HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Batch).WithMany(b => b.ProductListings).HasForeignKey(p => p.BatchId).OnDelete(DeleteBehavior.SetNull);
    }
}

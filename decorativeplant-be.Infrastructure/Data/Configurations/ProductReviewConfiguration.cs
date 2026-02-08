using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("product_review");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.Content).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.StatusInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.Images).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(p => p.Listing).WithMany(l => l.ProductReviews).HasForeignKey(p => p.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.User).WithMany(u => u.ProductReviews).HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.Order).WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

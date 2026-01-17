using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("product_review");

        builder.HasKey(pr => pr.Id);
        builder.Property(pr => pr.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pr => pr.ListingId)
            .IsRequired();

        builder.Property(pr => pr.UserId)
            .IsRequired();

        builder.Property(pr => pr.OrderId)
            .IsRequired();

        builder.Property(pr => pr.Rating)
            .IsRequired();

        builder.Property(pr => pr.ImagesJson)
            .HasColumnType("jsonb");

        builder.Property(pr => pr.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(pr => pr.Listing)
            .WithMany(l => l.ProductReviews)
            .HasForeignKey(pr => pr.ListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pr => pr.UserAccount)
            .WithMany(u => u.ProductReviews)
            .HasForeignKey(pr => pr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pr => pr.OrderHeader)
            .WithMany(o => o.ProductReviews)
            .HasForeignKey(pr => pr.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pr => pr.ListingId);
        builder.HasIndex(pr => pr.UserId);
    }
}

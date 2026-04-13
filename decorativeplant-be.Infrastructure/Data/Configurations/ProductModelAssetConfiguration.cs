using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ProductModelAssetConfiguration : IEntityTypeConfiguration<ProductModelAsset>
{
    public void Configure(EntityTypeBuilder<ProductModelAsset> builder)
    {
        builder.ToTable("product_model_asset");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProductListingId).IsRequired();
        builder.Property(x => x.GlbUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.DefaultScale).HasColumnType("numeric(10,4)").HasDefaultValue(1m);
        builder.Property(x => x.BoundingBox)
            .HasColumnType("jsonb")
            .HasConversion(JsonDocumentConverter.Instance);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.ProductListing)
            .WithMany()
            .HasForeignKey(x => x.ProductListingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProductListingId).IsUnique();
    }
}


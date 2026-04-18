using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlantTaxonomyConfiguration : IEntityTypeConfiguration<PlantTaxonomy>
{
    public void Configure(EntityTypeBuilder<PlantTaxonomy> builder)
    {
        builder.ToTable("plant_taxonomy");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.ScientificName).IsRequired().HasMaxLength(255);
        builder.HasIndex(p => p.ScientificName).IsUnique();
        builder.Property(p => p.CommonNames).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.TaxonomyInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.CareInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.GrowthInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        builder.HasOne(p => p.Category).WithMany(c => c.PlantTaxonomies).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}

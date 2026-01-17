using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlantTaxonomyConfiguration : IEntityTypeConfiguration<PlantTaxonomy>
{
    public void Configure(EntityTypeBuilder<PlantTaxonomy> builder)
    {
        builder.ToTable("plant_taxonomy");

        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pt => pt.ScientificName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(pt => pt.CommonName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(pt => pt.Cultivar)
            .HasMaxLength(100);

        builder.Property(pt => pt.Family)
            .HasMaxLength(100);
    }
}

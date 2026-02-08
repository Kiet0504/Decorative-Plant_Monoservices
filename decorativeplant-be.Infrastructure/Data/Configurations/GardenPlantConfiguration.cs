using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class GardenPlantConfiguration : IEntityTypeConfiguration<GardenPlant>
{
    public void Configure(EntityTypeBuilder<GardenPlant> builder)
    {
        builder.ToTable("garden_plant");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(g => g.Details).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(g => g.ImageUrl).HasMaxLength(500);
        builder.HasOne(g => g.User).WithMany(u => u.GardenPlants).HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(g => g.Taxonomy).WithMany(t => t.GardenPlants).HasForeignKey(g => g.TaxonomyId).OnDelete(DeleteBehavior.SetNull);
    }
}

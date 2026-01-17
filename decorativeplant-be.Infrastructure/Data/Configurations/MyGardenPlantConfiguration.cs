using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class MyGardenPlantConfiguration : IEntityTypeConfiguration<MyGardenPlant>
{
    public void Configure(EntityTypeBuilder<MyGardenPlant> builder)
    {
        builder.ToTable("my_garden_plant");

        builder.HasKey(mgp => mgp.Id);
        builder.Property(mgp => mgp.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(mgp => mgp.UserId)
            .IsRequired();

        builder.Property(mgp => mgp.SourceOrderItemId)
            .IsRequired();

        builder.Property(mgp => mgp.TaxonomyId)
            .IsRequired();

        builder.Property(mgp => mgp.Nickname)
            .HasMaxLength(100);

        builder.Property(mgp => mgp.HealthStatus)
            .HasMaxLength(50);

        builder.Property(mgp => mgp.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(mgp => mgp.UserAccount)
            .WithMany(u => u.MyGardenPlants)
            .HasForeignKey(mgp => mgp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mgp => mgp.SourceOrderItem)
            .WithMany(oi => oi.MyGardenPlants)
            .HasForeignKey(mgp => mgp.SourceOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(mgp => mgp.PlantTaxonomy)
            .WithMany(pt => pt.MyGardenPlants)
            .HasForeignKey(mgp => mgp.TaxonomyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mgp => mgp.UserId);
    }
}

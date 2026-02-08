using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class InventoryLocationConfiguration : IEntityTypeConfiguration<InventoryLocation>
{
    public void Configure(EntityTypeBuilder<InventoryLocation> builder)
    {
        builder.ToTable("inventory_location");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(i => i.Code).HasMaxLength(50);
        builder.Property(i => i.Name).HasMaxLength(255);
        builder.Property(i => i.Type).HasMaxLength(50);
        builder.Property(i => i.Details).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(i => i.Branch).WithMany(b => b.InventoryLocations).HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.ParentLocation).WithMany(i => i.ChildLocations).HasForeignKey(i => i.ParentLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

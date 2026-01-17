using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PlatformFeePolicyConfiguration : IEntityTypeConfiguration<PlatformFeePolicy>
{
    public void Configure(EntityTypeBuilder<PlatformFeePolicy> builder)
    {
        builder.ToTable("platform_fee_policy");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.FeeType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Value)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(p => p.ApplyScope)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        // Relationships
        builder.HasMany(p => p.SellerPackages)
            .WithOne(sp => sp.DefaultFeePolicy)
            .HasForeignKey(sp => sp.DefaultFeePolicyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

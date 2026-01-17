using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class SellerPackageConfiguration : IEntityTypeConfiguration<SellerPackage>
{
    public void Configure(EntityTypeBuilder<SellerPackage> builder)
    {
        builder.ToTable("seller_package");

        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(sp => sp.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sp => sp.Price)
            .IsRequired()
            .HasPrecision(12, 2);

        builder.Property(sp => sp.DurationDays)
            .IsRequired();

        builder.Property(sp => sp.BenefitsJson)
            .HasColumnType("jsonb");

        builder.Property(sp => sp.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(sp => sp.DefaultFeePolicy)
            .WithMany(pfp => pfp.SellerPackages)
            .HasForeignKey(sp => sp.DefaultFeePolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(sp => sp.Subscriptions)
            .WithOne(ss => ss.SellerPackage)
            .HasForeignKey(ss => ss.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(sp => sp.DefaultFeePolicyId);
    }
}

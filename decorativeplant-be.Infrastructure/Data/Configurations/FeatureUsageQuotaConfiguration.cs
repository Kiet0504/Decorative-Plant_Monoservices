using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class FeatureUsageQuotaConfiguration : IEntityTypeConfiguration<FeatureUsageQuota>
{
    public void Configure(EntityTypeBuilder<FeatureUsageQuota> builder)
    {
        builder.ToTable("feature_usage_quota");
        builder.HasKey(fq => fq.Id);
        builder.Property(fq => fq.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(fq => fq.UserId).IsRequired();
        builder.Property(fq => fq.FeatureKey).IsRequired().HasMaxLength(100);
        builder.Property(fq => fq.QuotaLimit).IsRequired();
        builder.Property(fq => fq.QuotaUsed).IsRequired().HasDefaultValue(0);
        builder.Property(fq => fq.QuotaPeriod).IsRequired().HasMaxLength(20);
        builder.Property(fq => fq.IsDeleted).HasDefaultValue(false);
        builder.Property(fq => fq.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(fq => fq.UserId);
        builder.HasIndex(fq => fq.FeatureKey);
        builder.HasIndex(fq => new { fq.UserId, fq.FeatureKey }).IsUnique();

        // Foreign key relationship
        builder.HasOne(fq => fq.User)
            .WithMany()
            .HasForeignKey(fq => fq.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class PremiumFeatureConfiguration : IEntityTypeConfiguration<PremiumFeature>
{
    public void Configure(EntityTypeBuilder<PremiumFeature> builder)
    {
        builder.ToTable("premium_feature");
        builder.HasKey(pf => pf.Id);
        builder.Property(pf => pf.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(pf => pf.FeatureKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(pf => pf.FeatureKey).IsUnique();

        builder.Property(pf => pf.FeatureName).IsRequired().HasMaxLength(255);
        builder.Property(pf => pf.Description).HasMaxLength(1000);
        builder.Property(pf => pf.AvailableInPlans).IsRequired().HasMaxLength(500);
        builder.Property(pf => pf.IsActive).HasDefaultValue(true);
        builder.Property(pf => pf.IsDeleted).HasDefaultValue(false);
        builder.Property(pf => pf.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(pf => pf.IsActive);
    }
}

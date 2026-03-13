using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("user_subscription");
        builder.HasKey(us => us.Id);
        builder.Property(us => us.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(us => us.UserId).IsRequired();
        builder.Property(us => us.PlanType).IsRequired().HasMaxLength(20);
        builder.Property(us => us.Status).IsRequired().HasMaxLength(20);
        builder.Property(us => us.StartAt).IsRequired();
        builder.Property(us => us.AutoRenew).HasDefaultValue(false);
        builder.Property(us => us.PaymentMethod).HasMaxLength(50);
        builder.Property(us => us.AmountPaid).HasMaxLength(50);
        builder.Property(us => us.BillingCycle).HasMaxLength(20);
        builder.Property(us => us.IsDeleted).HasDefaultValue(false);
        builder.Property(us => us.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(us => us.UserId);
        builder.HasIndex(us => us.Status);
        builder.HasIndex(us => new { us.UserId, us.Status });

        // Foreign key relationship
        builder.HasOne(us => us.User)
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

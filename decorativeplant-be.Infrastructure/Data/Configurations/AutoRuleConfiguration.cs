using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class AutoRuleConfiguration : IEntityTypeConfiguration<AutoRule>
{
    public void Configure(EntityTypeBuilder<AutoRule> builder)
    {
        builder.ToTable("auto_rule");

        builder.HasKey(ar => ar.Id);
        builder.Property(ar => ar.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ar => ar.DeviceId)
            .IsRequired();

        builder.Property(ar => ar.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(ar => ar.IsActive)
            .HasDefaultValue(true);

        builder.Property(ar => ar.Priority)
            .IsRequired();

        builder.Property(ar => ar.ConfigJson)
            .HasColumnType("jsonb");

        builder.Property(ar => ar.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(ar => ar.IotDevice)
            .WithMany(id => id.AutoRules)
            .HasForeignKey(ar => ar.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ar => ar.DeviceId);
    }
}

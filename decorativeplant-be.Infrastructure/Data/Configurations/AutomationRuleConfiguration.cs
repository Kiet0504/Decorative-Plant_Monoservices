using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("automation_rule");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.Name).HasMaxLength(255);
        builder.Property(a => a.Schedule).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(a => a.Conditions).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(a => a.Actions).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(a => a.Device).WithMany(d => d.AutomationRules).HasForeignKey(a => a.DeviceId).OnDelete(DeleteBehavior.SetNull);
    }
}

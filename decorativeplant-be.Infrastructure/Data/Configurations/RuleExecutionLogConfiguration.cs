using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class RuleExecutionLogConfiguration : IEntityTypeConfiguration<RuleExecutionLog>
{
    public void Configure(EntityTypeBuilder<RuleExecutionLog> builder)
    {
        builder.ToTable("rule_execution_log");

        builder.HasKey(rel => rel.Id);
        builder.Property(rel => rel.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(rel => rel.RuleId)
            .IsRequired();

        builder.Property(rel => rel.Result)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(rel => rel.TriggeredAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationships
        builder.HasOne(rel => rel.AutoRule)
            .WithMany(ar => ar.RuleExecutionLogs)
            .HasForeignKey(rel => rel.RuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rel => rel.RuleId);
    }
}

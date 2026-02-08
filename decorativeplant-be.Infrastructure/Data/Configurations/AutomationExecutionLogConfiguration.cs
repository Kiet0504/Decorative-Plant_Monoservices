using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class AutomationExecutionLogConfiguration : IEntityTypeConfiguration<AutomationExecutionLog>
{
    public void Configure(EntityTypeBuilder<AutomationExecutionLog> builder)
    {
        builder.ToTable("automation_execution_log");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.ExecutionInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(a => a.Rule).WithMany(r => r.ExecutionLogs).HasForeignKey(a => a.RuleId).OnDelete(DeleteBehavior.SetNull);
    }
}

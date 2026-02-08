using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class AiTrainingFeedbackConfiguration : IEntityTypeConfiguration<AiTrainingFeedback>
{
    public void Configure(EntityTypeBuilder<AiTrainingFeedback> builder)
    {
        builder.ToTable("ai_training_feedback");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.SourceInfo).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(a => a.FeedbackContent).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(a => a.User).WithMany(u => u.AiTrainingFeedbacks).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

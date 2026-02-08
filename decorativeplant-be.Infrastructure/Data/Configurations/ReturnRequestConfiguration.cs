using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("return_request");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(r => r.Status).HasMaxLength(50);
        builder.Property(r => r.Info).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(r => r.Images).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.HasOne(r => r.Order).WithMany(o => o.ReturnRequests).HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.User).WithMany(u => u.ReturnRequests).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class StaffAssignmentConfiguration : IEntityTypeConfiguration<StaffAssignment>
{
    public void Configure(EntityTypeBuilder<StaffAssignment> builder)
    {
        builder.ToTable("staff_assignment");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(s => s.StaffId).IsRequired();
        builder.Property(s => s.BranchId).IsRequired();
        builder.Property(s => s.Position).HasMaxLength(100);
        builder.Property(s => s.IsPrimary).HasDefaultValue(true);
        builder.Property(s => s.Permissions).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(s => s.AssignedAt).HasDefaultValueSql("now()");
        builder.HasOne(s => s.Staff).WithMany(u => u.StaffAssignments).HasForeignKey(s => s.StaffId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Branch).WithMany(b => b.StaffAssignments).HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Cascade);
    }
}

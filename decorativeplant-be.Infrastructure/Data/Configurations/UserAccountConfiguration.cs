using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_account");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.HasIndex(u => u.Phone).IsUnique();
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50);
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.EmailVerified).HasDefaultValue(false);
        builder.Property(u => u.DisplayName).HasMaxLength(255);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.LocationCity).HasMaxLength(100);
        builder.Property(u => u.HardinessZone).HasMaxLength(10);
        builder.Property(u => u.ExperienceLevel).HasMaxLength(50);
        builder.Property(u => u.Addresses).HasColumnType("jsonb").HasConversion(JsonDocumentConverter.Instance);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
    }
}

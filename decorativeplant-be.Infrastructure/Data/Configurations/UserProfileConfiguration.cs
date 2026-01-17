using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Infrastructure.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profile");

        builder.HasKey(p => p.UserId);

        builder.Property(p => p.DisplayName)
            .HasMaxLength(255);

        builder.Property(p => p.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(p => p.AddressJson)
            .HasColumnType("jsonb");

        builder.Property(p => p.PreferencesJson)
            .HasColumnType("jsonb");

        builder.Property(p => p.HardinessZone)
            .HasMaxLength(10);

        builder.Property(p => p.ExperienceLevel)
            .HasMaxLength(50);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relationship
        builder.HasOne(p => p.UserAccount)
            .WithOne(u => u.UserProfile)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

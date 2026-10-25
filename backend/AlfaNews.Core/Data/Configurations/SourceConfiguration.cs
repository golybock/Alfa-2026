using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class SourceConfiguration : IEntityTypeConfiguration<SourceEntity>
{
    public void Configure(EntityTypeBuilder<SourceEntity> builder)
    {
        builder.ToTable("source", table =>
        {
            table.HasCheckConstraint("source_1_check", "kind IN ('official','media')");
            table.HasCheckConstraint("source_2_check", "poll_seconds >= 60");
            table.HasCheckConstraint("source_3_check", "reliability BETWEEN 0 AND 1");
        });
        builder.HasKey(entity => entity.Id).HasName("source_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.FeedUrl).HasColumnName("feed_url").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.AllowedHosts).HasColumnName("allowed_hosts").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.Enabled).HasColumnName("enabled").HasColumnType("boolean").IsRequired()
            .HasDefaultValue(false);
        builder.Property(entity => entity.IsSpecialized).HasColumnName("is_specialized").HasDefaultValue(false);
        builder.Property(entity => entity.PollSeconds).HasColumnName("poll_seconds").HasColumnType("integer")
            .IsRequired().HasDefaultValue(3600);
        builder.Property(entity => entity.Reliability).HasColumnName("reliability").HasColumnType("numeric(3,2)")
            .IsRequired();
        builder.Property(entity => entity.ValidationStatus).HasColumnName("validation_status").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.LastCheckedAt).HasColumnName("last_checked_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.LastSuccessAt).HasColumnName("last_success_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasColumnType("text");
    }
}
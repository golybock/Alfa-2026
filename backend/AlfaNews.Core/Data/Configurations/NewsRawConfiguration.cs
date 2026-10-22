using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class NewsRawConfiguration : IEntityTypeConfiguration<NewsRawEntity>
{
    public void Configure(EntityTypeBuilder<NewsRawEntity> builder)
    {
        builder.ToTable("news_raw", table =>
        {
            table.HasCheckConstraint("news_raw_1_check", "status IN ('queued','processing','accepted','duplicate','rejected','failed')");
        });
        builder.HasKey(entity => entity.Id).HasName("news_raw_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.SourceId).HasColumnName("source_id").HasColumnType("text").IsRequired();
        builder.HasOne<SourceEntity>().WithMany().HasForeignKey(entity => entity.SourceId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("news_raw_source_id_fkey");
        builder.Property(entity => entity.CanonicalUrl).HasColumnName("canonical_url").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Title).HasColumnName("title").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Text).HasColumnName("text").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.TextSha256).HasColumnName("text_sha256").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.FetchedAt).HasColumnName("fetched_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.RejectionReason).HasColumnName("rejection_reason").HasColumnType("text");
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasColumnType("text");
        builder.HasAlternateKey(entity => entity.CanonicalUrl).HasName("news_raw_canonical_url_key");
        builder.HasIndex(entity => entity.TextSha256).HasDatabaseName("news_hash_idx");
        builder.HasIndex(entity => entity.PublishedAt).HasDatabaseName("news_date_idx").IsDescending(true);
    }
}
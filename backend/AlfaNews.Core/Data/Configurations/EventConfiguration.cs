using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<EventEntity>
{
    public void Configure(EntityTypeBuilder<EventEntity> builder)
    {
        builder.ToTable("event");
        builder.HasKey(entity => entity.Id).HasName("event_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.NewsRawId).HasColumnName("news_raw_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<NewsRawEntity>().WithMany().HasForeignKey(entity => entity.NewsRawId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("event_news_raw_id_fkey");
        builder.Property(entity => entity.ContentSha256).HasColumnName("content_sha256").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.Headline).HasColumnName("headline").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.EventDate).HasColumnName("event_date").HasColumnType("date");
        builder.Property(entity => entity.PublishedAt).HasColumnName("published_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.ExtractionJson).HasColumnName("extraction_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.InputVersion).HasColumnName("input_version").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.SearchText).HasColumnName("search_text").HasColumnType("tsvector")
            .IsRequired();
        builder.Property(entity => entity.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.HasAlternateKey(entity => entity.NewsRawId).HasName("event_news_raw_id_key");
        builder.HasAlternateKey(entity => entity.ContentSha256).HasName("event_content_sha256_key");
        builder.HasIndex(entity => entity.PublishedAt).HasDatabaseName("event_date_idx").IsDescending(true);
        builder.HasIndex(entity => entity.SearchText).HasDatabaseName("event_fts_idx").HasMethod("gin");
    }
}
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class EventSourceConfiguration : IEntityTypeConfiguration<EventSourceEntity>
{
    public void Configure(EntityTypeBuilder<EventSourceEntity> builder)
    {
        builder.ToTable("event_source");
        builder.HasKey(entity => new { entity.EventId, entity.NewsRawId }).HasName("event_source_pkey");
        builder.Property(entity => entity.EventId).HasColumnName("event_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<EventEntity>().WithMany().HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("event_source_event_id_fkey");
        builder.Property(entity => entity.NewsRawId).HasColumnName("news_raw_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<NewsRawEntity>().WithMany().HasForeignKey(entity => entity.NewsRawId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("event_source_news_raw_id_fkey");
    }
}
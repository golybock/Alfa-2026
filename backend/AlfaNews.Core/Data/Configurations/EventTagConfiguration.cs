using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class EventTagConfiguration : IEntityTypeConfiguration<EventTagEntity>
{
    public void Configure(EntityTypeBuilder<EventTagEntity> builder)
    {
        builder.ToTable("event_tag",
            table =>
            {
                table.HasCheckConstraint("event_tag_1_check",
                    "kind IN ('factor','okved2','country','region','commodity')");
            });
        builder.HasKey(entity => new { entity.EventId, entity.Kind, entity.Code }).HasName("event_tag_pkey");
        builder.Property(entity => entity.EventId).HasColumnName("event_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<EventEntity>().WithMany().HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("event_tag_event_id_fkey");
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Code).HasColumnName("code").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.HasIndex(entity => new { entity.Kind, entity.Code }).HasDatabaseName("event_tag_lookup_idx");
    }
}
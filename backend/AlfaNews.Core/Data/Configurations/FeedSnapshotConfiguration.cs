using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class FeedSnapshotConfiguration : IEntityTypeConfiguration<FeedSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<FeedSnapshotEntity> builder)
    {
        builder.ToTable("feed_snapshot", table =>
        {
            table.HasCheckConstraint("feed_snapshot_1_check", "mode IN ('live','demo')");
        });
        builder.HasKey(entity => new { entity.CompanyId, entity.Mode }).HasName("feed_snapshot_pkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("feed_snapshot_company_id_fkey");
        builder.Property(entity => entity.Mode).HasColumnName("mode").HasColumnType("text").IsRequired().HasDefaultValueSql("'live'");
        builder.Property(entity => entity.AsOf).HasColumnName("as_of").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.NewsPeriodFrom).HasColumnName("news_period_from").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.NewsPeriodTo).HasColumnName("news_period_to").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.InputVersion).HasColumnName("input_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ItemsJson).HasColumnName("items_json").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.LastUpdatedAt).HasColumnName("last_updated_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.CollectionStartedAt).HasColumnName("collection_started_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.RefreshStatus).HasColumnName("refresh_status").HasColumnType("text").IsRequired();
    }
}
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class ResearchChunkConfiguration : IEntityTypeConfiguration<ResearchChunkEntity>
{
    public void Configure(EntityTypeBuilder<ResearchChunkEntity> builder)
    {
        builder.ToTable("research_chunk", table =>
        {
            table.HasCheckConstraint("research_chunk_1_check", "kind IN ('fact','bank_view','forecast','disclaimer')");
            table.HasCheckConstraint("research_chunk_2_check",
                "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
        });
        builder.HasKey(entity => entity.Id).HasName("research_chunk_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.PageId).HasColumnName("page_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<ResearchPageEntity>().WithMany().HasForeignKey(entity => entity.PageId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("research_chunk_page_id_fkey");
        builder.Property(entity => entity.LocalId).HasColumnName("local_id").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Text).HasColumnName("text").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.TablesJson).HasColumnName("tables_json").HasColumnType("jsonb").IsRequired()
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(entity => entity.ChartData).HasColumnName("chart_data").HasColumnType("jsonb").IsRequired()
            .HasDefaultValueSql("'[]'::jsonb");
        builder.Property(entity => entity.TagsJson).HasColumnName("tags_json").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.ReviewStatus).HasColumnName("review_status").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.ReviewNote).HasColumnName("review_note").HasColumnType("text");
        builder.Property(entity => entity.Version).HasColumnName("version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.SearchText).HasColumnName("search_text").HasColumnType("tsvector")
            .IsRequired();
        builder.Property(entity => entity.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
        builder.HasAlternateKey(entity => new { entity.PageId, entity.LocalId })
            .HasName("research_chunk_page_id_local_id_key");
        builder.HasIndex(entity => entity.SearchText).HasDatabaseName("research_fts_idx").HasMethod("gin");
    }
}
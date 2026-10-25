using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class ResearchPageConfiguration : IEntityTypeConfiguration<ResearchPageEntity>
{
    public void Configure(EntityTypeBuilder<ResearchPageEntity> builder)
    {
        builder.ToTable("research_page", table =>
        {
            table.HasCheckConstraint("research_page_1_check", "page_no > 0");
            table.HasCheckConstraint("research_page_2_check", "ocr_status IN ('queued','processing','ready','failed')");
            table.HasCheckConstraint("research_page_3_check",
                "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
        });
        builder.HasKey(entity => entity.Id).HasName("research_page_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.DocumentId).HasColumnName("document_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<ResearchDocEntity>().WithMany().HasForeignKey(entity => entity.DocumentId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("research_page_document_id_fkey");
        builder.Property(entity => entity.PageNo).HasColumnName("page_no").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.ImagePath).HasColumnName("image_path").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.OcrStatus).HasColumnName("ocr_status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.OcrJson).HasColumnName("ocr_json").HasColumnType("jsonb");
        builder.Property(entity => entity.ReviewStatus).HasColumnName("review_status").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid");
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.ReviewedBy)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("research_page_reviewed_by_fkey");
        builder.Property(entity => entity.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasColumnType("text");
        builder.HasAlternateKey(entity => new { entity.DocumentId, entity.PageNo })
            .HasName("research_page_document_id_page_no_key");
    }
}
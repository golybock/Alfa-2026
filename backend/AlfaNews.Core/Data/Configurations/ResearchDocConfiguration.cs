using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class ResearchDocConfiguration : IEntityTypeConfiguration<ResearchDocEntity>
{
    public void Configure(EntityTypeBuilder<ResearchDocEntity> builder)
    {
        builder.ToTable("research_doc", table =>
        {
            table.HasCheckConstraint("research_doc_1_check", "page_count > 0");
            table.HasCheckConstraint("research_doc_2_check", "ocr_status IN ('queued','processing','ready','partial','failed')");
            table.HasCheckConstraint("research_doc_3_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
        });
        builder.HasKey(entity => entity.Id).HasName("research_doc_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.Sha256).HasColumnName("sha256").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.FilePath).HasColumnName("file_path").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Title).HasColumnName("title").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.SourceUrl).HasColumnName("source_url").HasColumnType("text");
        builder.Property(entity => entity.PublishedOn).HasColumnName("published_on").HasColumnType("date");
        builder.Property(entity => entity.PageCount).HasColumnName("page_count").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.OcrStatus).HasColumnName("ocr_status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ReviewStatus).HasColumnName("review_status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.UploadedBy).HasColumnName("uploaded_by").HasColumnType("uuid");
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UploadedBy).OnDelete(DeleteBehavior.NoAction).HasConstraintName("research_doc_uploaded_by_fkey");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.HasAlternateKey(entity => entity.Sha256).HasName("research_doc_sha256_key");
    }
}
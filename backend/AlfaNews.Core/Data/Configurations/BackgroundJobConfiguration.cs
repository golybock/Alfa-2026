using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJobEntity>
{
    public void Configure(EntityTypeBuilder<BackgroundJobEntity> builder)
    {
        builder.ToTable("background_job",
            table =>
            {
                table.HasCheckConstraint("background_job_1_check", "status IN ('queued','running','done','failed')");
            });
        builder.HasKey(entity => entity.Id).HasName("background_job_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Priority).HasColumnName("priority").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.OwnerUserId).HasColumnName("owner_user_id").HasColumnType("uuid");
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("background_job_owner_user_id_fkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text");
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("background_job_company_id_fkey");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamptz");
        builder.Property(entity => entity.Attempts).HasColumnName("attempts").HasColumnType("integer").IsRequired()
            .HasDefaultValue(0);
        builder.Property(entity => entity.Stage).HasColumnName("stage").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ResultType).HasColumnName("result_type").HasColumnType("text");
        builder.Property(entity => entity.ResultId).HasColumnName("result_id").HasColumnType("text");
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasColumnType("text");
        builder.HasIndex(entity => new { entity.Status, entity.Priority, entity.CreatedAt })
            .HasDatabaseName("job_queue_idx");
    }
}
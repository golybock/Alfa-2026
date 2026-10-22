using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRunEntity>
{
    public void Configure(EntityTypeBuilder<AgentRunEntity> builder)
    {
        builder.ToTable("agent_run");
        builder.HasKey(entity => entity.Id).HasName("agent_run_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.JobId).HasColumnName("job_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<BackgroundJobEntity>().WithMany().HasForeignKey(entity => entity.JobId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("agent_run_job_id_fkey");
        builder.Property(entity => entity.InsightId).HasColumnName("insight_id").HasColumnType("uuid");
        builder.HasOne<InsightEntity>().WithMany().HasForeignKey(entity => entity.InsightId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("agent_run_insight_id_fkey");
        builder.Property(entity => entity.InputVersion).HasColumnName("input_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamptz");
    }
}
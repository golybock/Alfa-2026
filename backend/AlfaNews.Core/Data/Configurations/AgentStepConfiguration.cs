using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class AgentStepConfiguration : IEntityTypeConfiguration<AgentStepEntity>
{
    public void Configure(EntityTypeBuilder<AgentStepEntity> builder)
    {
        builder.ToTable("agent_step");
        builder.HasKey(entity => entity.Id).HasName("agent_step_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.RunId).HasColumnName("run_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(entity => entity.RunId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("agent_step_run_id_fkey");
        builder.Property(entity => entity.Ordinal).HasColumnName("ordinal").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.Stage).HasColumnName("stage").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ToolName).HasColumnName("tool_name").HasColumnType("text");
        builder.Property(entity => entity.ArgumentsJson).HasColumnName("arguments_json").HasColumnType("jsonb");
        builder.Property(entity => entity.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");
        builder.Property(entity => entity.EvidenceRefs).HasColumnName("evidence_refs").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'[]'::jsonb");
        builder.Property(entity => entity.Summary).HasColumnName("summary").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.DurationMs).HasColumnName("duration_ms").HasColumnType("bigint").IsRequired();
        builder.HasAlternateKey(entity => new { entity.RunId, entity.Ordinal }).HasName("agent_step_run_id_ordinal_key");
    }
}
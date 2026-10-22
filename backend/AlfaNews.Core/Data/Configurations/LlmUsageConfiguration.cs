using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class LlmUsageConfiguration : IEntityTypeConfiguration<LlmUsageEntity>
{
    public void Configure(EntityTypeBuilder<LlmUsageEntity> builder)
    {
        builder.ToTable("llm_usage");
        builder.HasKey(entity => entity.Id).HasName("llm_usage_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.RunId).HasColumnName("run_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(entity => entity.RunId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("llm_usage_run_id_fkey");
        builder.Property(entity => entity.CardId).HasColumnName("card_id").HasColumnType("uuid");
        builder.HasOne<InsightEntity>().WithMany().HasForeignKey(entity => entity.CardId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("llm_usage_card_id_fkey");
        builder.Property(entity => entity.Stage).HasColumnName("stage").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Model).HasColumnName("model").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ConfigVersion).HasColumnName("config_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Attempt).HasColumnName("attempt").HasColumnType("integer").IsRequired();
        builder.Property(entity => entity.InputTokens).HasColumnName("input_tokens").HasColumnType("bigint").IsRequired();
        builder.Property(entity => entity.OutputTokens).HasColumnName("output_tokens").HasColumnType("bigint").IsRequired();
        builder.Property(entity => entity.ReasoningTokens).HasColumnName("reasoning_tokens").HasColumnType("bigint");
        builder.Property(entity => entity.ActualCostRub).HasColumnName("actual_cost_rub").HasColumnType("numeric(18,8)");
        builder.Property(entity => entity.ReservedCostRub).HasColumnName("reserved_cost_rub").HasColumnType("numeric(18,8)").HasDefaultValue(0m);
        builder.Property(entity => entity.RequestKey).HasColumnName("request_key").HasColumnType("text").HasDefaultValue("");
        builder.HasIndex(entity => new { entity.RequestKey, entity.Stage }).HasDatabaseName("usage_request_stage_idx");
        builder.Property(entity => entity.DurationMs).HasColumnName("duration_ms").HasColumnType("bigint").IsRequired();
        builder.Property(entity => entity.CacheHit).HasColumnName("cache_hit").HasColumnType("boolean").IsRequired();
        builder.Property(entity => entity.Error).HasColumnName("error").HasColumnType("text");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(entity => entity.CreatedAt).HasDatabaseName("usage_date_idx");
    }
}
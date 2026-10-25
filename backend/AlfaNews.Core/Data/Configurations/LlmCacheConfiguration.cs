using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class LlmCacheConfiguration : IEntityTypeConfiguration<LlmCacheEntity>
{
    public void Configure(EntityTypeBuilder<LlmCacheEntity> builder)
    {
        builder.ToTable("llm_cache");
        builder.HasKey(entity => entity.CacheKey).HasName("llm_cache_pkey");
        builder.Property(entity => entity.CacheKey).HasColumnName("cache_key").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Task).HasColumnName("task").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Model).HasColumnName("model").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.SchemaVersion).HasColumnName("schema_version").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .IsRequired();
    }
}
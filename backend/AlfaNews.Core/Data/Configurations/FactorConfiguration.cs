using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class FactorConfiguration : IEntityTypeConfiguration<FactorEntity>
{
    public void Configure(EntityTypeBuilder<FactorEntity> builder)
    {
        builder.ToTable("factor");
        builder.HasKey(entity => entity.Code).HasName("factor_pkey");
        builder.Property(entity => entity.Code).HasColumnName("code").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Keywords).HasColumnName("keywords").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'[]'::jsonb");
        builder.Property(entity => entity.Version).HasColumnName("version").HasColumnType("text").IsRequired();
    }
}
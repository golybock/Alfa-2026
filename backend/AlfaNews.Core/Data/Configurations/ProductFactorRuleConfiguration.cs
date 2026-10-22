using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class ProductFactorRuleConfiguration : IEntityTypeConfiguration<ProductFactorRuleEntity>
{
    public void Configure(EntityTypeBuilder<ProductFactorRuleEntity> builder)
    {
        builder.ToTable("product_factor_rule");
        builder.HasKey(entity => new { entity.ProductCode, entity.FactorCode }).HasName("product_factor_rule_pkey");
        builder.Property(entity => entity.ProductCode).HasColumnName("product_code").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.FactorCode).HasColumnName("factor_code").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.HasOne<FactorEntity>().WithMany().HasForeignKey(entity => entity.FactorCode).OnDelete(DeleteBehavior.NoAction).HasConstraintName("product_factor_rule_factor_code_fkey");
        builder.Property(entity => entity.Channel).HasColumnName("channel").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasColumnType("text").IsRequired();
    }
}
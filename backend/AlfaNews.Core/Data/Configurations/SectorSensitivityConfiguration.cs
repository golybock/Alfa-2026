using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class SectorSensitivityConfiguration : IEntityTypeConfiguration<SectorSensitivityEntity>
{
    public void Configure(EntityTypeBuilder<SectorSensitivityEntity> builder)
    {
        builder.ToTable("sector_sensitivity", table =>
        {
            table.HasCheckConstraint("sector_sensitivity_1_check", "weight BETWEEN 0 AND 1");
            table.HasCheckConstraint("sector_sensitivity_2_check", "sign IN ('positive','negative','mixed')");
        });
        builder.HasKey(entity => new { entity.Okved2, entity.FactorCode }).HasName("sector_sensitivity_pkey");
        builder.Property(entity => entity.Okved2).HasColumnName("okved2").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.FactorCode).HasColumnName("factor_code").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<FactorEntity>().WithMany().HasForeignKey(entity => entity.FactorCode)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("sector_sensitivity_factor_code_fkey");
        builder.Property(entity => entity.Weight).HasColumnName("weight").HasColumnType("numeric(6,5)").IsRequired();
        builder.Property(entity => entity.Sign).HasColumnName("sign").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Channel).HasColumnName("channel").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.Version).HasColumnName("version").HasColumnType("text").IsRequired();
    }
}
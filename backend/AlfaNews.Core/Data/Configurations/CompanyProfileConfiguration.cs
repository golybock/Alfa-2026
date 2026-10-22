using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfileEntity>
{
    public void Configure(EntityTypeBuilder<CompanyProfileEntity> builder)
    {
        builder.ToTable("company_profile");
        builder.HasKey(entity => entity.CompanyId).HasName("company_profile_pkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("company_profile_company_id_fkey");
        builder.Property(entity => entity.DataVersion).HasColumnName("data_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ProfileJson).HasColumnName("profile_json").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
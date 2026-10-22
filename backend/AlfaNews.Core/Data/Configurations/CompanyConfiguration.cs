using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<CompanyEntity>
{
    public void Configure(EntityTypeBuilder<CompanyEntity> builder)
    {
        builder.ToTable("company", table =>
        {
            table.HasCheckConstraint("company_1_check", "cl_type = 'CLIENT'");
        });
        builder.HasKey(entity => entity.Id).HasName("company_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("text").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.ClType).HasColumnName("cl_type").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.GroupId).HasColumnName("group_id").HasColumnType("text");
        builder.Property(entity => entity.MainOkvedCode).HasColumnName("main_okved_code").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.MainOkvedName).HasColumnName("main_okved_name").HasColumnType("text");
        builder.Property(entity => entity.Region).HasColumnName("region").HasColumnType("text");
        builder.Property(entity => entity.City).HasColumnName("city").HasColumnType("text");
        builder.Property(entity => entity.Revenue).HasColumnName("revenue").HasColumnType("numeric(24,2)");
        builder.Property(entity => entity.RawData).HasColumnName("raw_data").HasColumnType("jsonb").IsRequired();
        builder.Property(entity => entity.DatasetVersion).HasColumnName("dataset_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.QualityFlags).HasColumnName("quality_flags").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'[]'::jsonb");
        builder.Property(entity => entity.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(entity => entity.GroupId).HasDatabaseName("company_group_idx");
    }
}
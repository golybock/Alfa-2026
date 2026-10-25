using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class UserCompanyAccessConfiguration : IEntityTypeConfiguration<UserCompanyAccessEntity>
{
    public void Configure(EntityTypeBuilder<UserCompanyAccessEntity> builder)
    {
        builder.ToTable("user_company_access",
            table => { table.HasCheckConstraint("user_company_access_1_check", "role IN ('ceo','cfo')"); });
        builder.HasKey(entity => new { entity.UserId, entity.CompanyId, entity.Role })
            .HasName("user_company_access_pkey");
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("user_company_access_user_id_fkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("user_company_access_company_id_fkey");
        builder.Property(entity => entity.Role).HasColumnName("role").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
    }
}
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUserEntity>
{
    public void Configure(EntityTypeBuilder<AppUserEntity> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(entity => entity.Id).HasName("app_user_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Login).HasColumnName("login").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.PasswordHash).HasColumnName("password_hash").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.DisplayName).HasColumnName("display_name").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.IsOperator).HasColumnName("is_operator").HasColumnType("boolean").IsRequired()
            .HasDefaultValue(false);
        builder.Property(entity => entity.Active).HasColumnName("active").HasColumnType("boolean").IsRequired()
            .HasDefaultValue(true);
        builder.HasAlternateKey(entity => entity.Login).HasName("app_user_login_key");
    }
}
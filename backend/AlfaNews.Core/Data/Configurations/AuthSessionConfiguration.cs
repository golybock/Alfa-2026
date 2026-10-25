using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSessionEntity>
{
    public void Configure(EntityTypeBuilder<AuthSessionEntity> builder)
    {
        builder.ToTable("auth_session");
        builder.HasKey(entity => entity.Id).HasName("auth_session_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("auth_session_user_id_fkey");
        builder.Property(entity => entity.TokenHash).HasColumnName("token_hash").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz");
        builder.HasAlternateKey(entity => entity.TokenHash).HasName("auth_session_token_hash_key");
    }
}
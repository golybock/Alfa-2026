using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackEntity>
{
    public void Configure(EntityTypeBuilder<FeedbackEntity> builder)
    {
        builder.ToTable("feedback",
            table => { table.HasCheckConstraint("feedback_1_check", "kind IN ('like','dislike','hide')"); });
        builder.HasKey(entity => new { entity.UserId, entity.CompanyId, entity.EventId }).HasName("feedback_pkey");
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("feedback_user_id_fkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("feedback_company_id_fkey");
        builder.Property(entity => entity.EventId).HasColumnName("event_id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.HasOne<EventEntity>().WithMany().HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("feedback_event_id_fkey");
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz")
            .IsRequired();
    }
}
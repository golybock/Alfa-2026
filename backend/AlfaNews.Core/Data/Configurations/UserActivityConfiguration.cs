using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class UserActivityConfiguration : IEntityTypeConfiguration<UserActivityEntity>
{
    public void Configure(EntityTypeBuilder<UserActivityEntity> builder)
    {
        builder.ToTable("user_activity", table =>
        {
            table.HasCheckConstraint("user_activity_1_check", "kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted')");
            table.HasCheckConstraint("user_activity_2_check", "(kind = 'source_opened') = (evidence_id IS NOT NULL)");
            table.HasCheckConstraint("user_activity_3_check", "(kind = 'action_interacted') = (action_id IS NOT NULL)");
        });
        builder.HasKey(entity => entity.Id).HasName("user_activity_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("user_activity_user_id_fkey");
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("user_activity_company_id_fkey");
        builder.Property(entity => entity.InsightId).HasColumnName("insight_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<InsightEntity>().WithMany().HasForeignKey(entity => entity.InsightId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("user_activity_insight_id_fkey");
        builder.Property(entity => entity.Kind).HasColumnName("kind").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.EvidenceId).HasColumnName("evidence_id").HasColumnType("text");
        builder.Property(entity => entity.ActionId).HasColumnName("action_id").HasColumnType("text");
        builder.HasIndex(entity => new { entity.UserId, entity.InsightId, entity.CreatedAt }).HasDatabaseName("activity_user_insight_idx");
    }
}
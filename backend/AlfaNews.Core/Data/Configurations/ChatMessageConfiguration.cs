using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessageEntity>
{
    public void Configure(EntityTypeBuilder<ChatMessageEntity> builder)
    {
        builder.ToTable("chat_message", table =>
        {
            table.HasCheckConstraint("chat_message_1_check", "status IN ('queued','processing','ready','failed')");
        });
        builder.HasKey(entity => entity.Id).HasName("chat_message_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired().ValueGeneratedNever();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<AppUserEntity>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("chat_message_user_id_fkey");
        builder.Property(entity => entity.InsightId).HasColumnName("insight_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<InsightEntity>().WithMany().HasForeignKey(entity => entity.InsightId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("chat_message_insight_id_fkey");
        builder.Property(entity => entity.Question).HasColumnName("question").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.AnswerJson).HasColumnName("answer_json").HasColumnType("jsonb");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.HasIndex(entity => new { entity.UserId, entity.InsightId, entity.CreatedAt }).HasDatabaseName("chat_history_idx");
    }
}
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class InsightConfiguration : IEntityTypeConfiguration<InsightEntity>
{
    public void Configure(EntityTypeBuilder<InsightEntity> builder)
    {
        builder.ToTable("insight", table =>
        {
            table.HasCheckConstraint("insight_1_check", "role IN ('ceo','cfo')");
            table.HasCheckConstraint("insight_2_check", "status IN ('queued','processing','ready','failed')");
        });
        builder.HasKey(entity => entity.Id).HasName("insight_pkey");
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("uuid").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.CompanyId).HasColumnName("company_id").HasColumnType("text").IsRequired();
        builder.HasOne<CompanyEntity>().WithMany().HasForeignKey(entity => entity.CompanyId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("insight_company_id_fkey");
        builder.Property(entity => entity.EventId).HasColumnName("event_id").HasColumnType("uuid").IsRequired();
        builder.HasOne<EventEntity>().WithMany().HasForeignKey(entity => entity.EventId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("insight_event_id_fkey");
        builder.Property(entity => entity.Role).HasColumnName("role").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.InputVersion).HasColumnName("input_version").HasColumnType("text")
            .IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.BodyJson).HasColumnName("body_json").HasColumnType("jsonb");
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasColumnType("text");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz")
            .IsRequired();
        builder.HasAlternateKey(entity => new { entity.CompanyId, entity.EventId, entity.Role, entity.InputVersion })
            .HasName("insight_company_id_event_id_role_input_version_key");
    }
}
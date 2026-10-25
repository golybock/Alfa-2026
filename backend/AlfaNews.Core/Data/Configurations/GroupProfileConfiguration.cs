using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class GroupProfileConfiguration : IEntityTypeConfiguration<GroupProfileEntity>
{
    public void Configure(EntityTypeBuilder<GroupProfileEntity> builder)
    {
        builder.ToTable("group_profile");
        builder.HasKey(entity => entity.GroupId).HasName("group_profile_pkey");
        builder.Property(entity => entity.GroupId).HasColumnName("group_id").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.DataVersion).HasColumnName("data_version").HasColumnType("text").IsRequired();
        builder.Property(entity => entity.ProfileJson).HasColumnName("profile_json").HasColumnType("jsonb")
            .IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz")
            .IsRequired();
    }
}
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlfaNews.Core.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("payment", table =>
        {
            table.HasCheckConstraint("payment_1_check", "pay_cnt >= 0");
            table.HasCheckConstraint("payment_2_check", "payer <> beneficiary");
        });
        builder.HasKey(entity => new { entity.Payer, entity.Beneficiary }).HasName("payment_pkey");
        builder.Property(entity => entity.Payer).HasColumnName("payer").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.Beneficiary).HasColumnName("beneficiary").HasColumnType("text").IsRequired()
            .ValueGeneratedNever();
        builder.Property(entity => entity.PayAmt).HasColumnName("pay_amt").HasColumnType("numeric(28,2)").IsRequired();
        builder.Property(entity => entity.PayCnt).HasColumnName("pay_cnt").HasColumnType("bigint").IsRequired();
        builder.Property(entity => entity.DatasetVersion).HasColumnName("dataset_version").HasColumnType("text")
            .IsRequired();
        builder.HasIndex(entity => entity.Beneficiary).HasDatabaseName("payment_beneficiary_idx");
    }
}
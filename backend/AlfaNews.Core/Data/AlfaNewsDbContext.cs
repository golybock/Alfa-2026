using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace AlfaNews.Core.Data;

public sealed class AlfaNewsDbContext(DbContextOptions<AlfaNewsDbContext> options) : DbContext(options)
{
    public DbSet<CompanyEntity> CompanyRows => Set<CompanyEntity>();
    public DbSet<PaymentEntity> PaymentRows => Set<PaymentEntity>();
    public DbSet<CompanyProfileEntity> CompanyProfileRows => Set<CompanyProfileEntity>();
    public DbSet<GroupProfileEntity> GroupProfileRows => Set<GroupProfileEntity>();
    public DbSet<FactorEntity> FactorRows => Set<FactorEntity>();
    public DbSet<SectorSensitivityEntity> SectorSensitivityRows => Set<SectorSensitivityEntity>();
    public DbSet<ProductFactorRuleEntity> ProductFactorRuleRows => Set<ProductFactorRuleEntity>();
    public DbSet<AppUserEntity> AppUserRows => Set<AppUserEntity>();
    public DbSet<AuthSessionEntity> AuthSessionRows => Set<AuthSessionEntity>();
    public DbSet<UserCompanyAccessEntity> UserCompanyAccessRows => Set<UserCompanyAccessEntity>();
    public DbSet<SourceEntity> SourceRows => Set<SourceEntity>();
    public DbSet<NewsRawEntity> NewsRawRows => Set<NewsRawEntity>();
    public DbSet<EventEntity> EventRows => Set<EventEntity>();
    public DbSet<EventSourceEntity> EventSourceRows => Set<EventSourceEntity>();
    public DbSet<EventTagEntity> EventTagRows => Set<EventTagEntity>();
    public DbSet<ResearchDocEntity> ResearchDocRows => Set<ResearchDocEntity>();
    public DbSet<ResearchPageEntity> ResearchPageRows => Set<ResearchPageEntity>();
    public DbSet<ResearchChunkEntity> ResearchChunkRows => Set<ResearchChunkEntity>();
    public DbSet<FeedSnapshotEntity> FeedSnapshotRows => Set<FeedSnapshotEntity>();
    public DbSet<InsightEntity> InsightRows => Set<InsightEntity>();
    public DbSet<ChatMessageEntity> ChatMessageRows => Set<ChatMessageEntity>();
    public DbSet<FeedbackEntity> FeedbackRows => Set<FeedbackEntity>();
    public DbSet<BackgroundJobEntity> BackgroundJobRows => Set<BackgroundJobEntity>();
    public DbSet<AgentRunEntity> AgentRunRows => Set<AgentRunEntity>();
    public DbSet<AgentStepEntity> AgentStepRows => Set<AgentStepEntity>();
    public DbSet<LlmUsageEntity> LlmUsageRows => Set<LlmUsageEntity>();
    public DbSet<LlmCacheEntity> LlmCacheRows => Set<LlmCacheEntity>();
    public DbSet<UserActivityEntity> UserActivityRows => Set<UserActivityEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlfaNewsDbContext).Assembly);
    }
}
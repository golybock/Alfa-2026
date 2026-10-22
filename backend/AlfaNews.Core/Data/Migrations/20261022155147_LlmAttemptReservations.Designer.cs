using System;
using System.Text.Json;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace AlfaNews.Core.Data.Migrations;
[DbContext(typeof(AlfaNewsDbContext))]
[Migration("20261022155147_LlmAttemptReservations")]
partial class LlmAttemptReservations
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.4")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "vector");
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AgentRunEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset?>("FinishedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("finished_at");

                b.Property<string>("InputVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("input_version");

                b.Property<Guid?>("InsightId")
                    .HasColumnType("uuid")
                    .HasColumnName("insight_id");

                b.Property<Guid>("JobId")
                    .HasColumnType("uuid")
                    .HasColumnName("job_id");

                b.Property<DateTimeOffset>("StartedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("started_at");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("status");

                b.HasKey("Id")
                    .HasName("agent_run_pkey");

                b.ToTable("agent_run", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AgentStepEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<long>("DurationMs")
                    .HasColumnType("bigint")
                    .HasColumnName("duration_ms");

                b.Property<JsonElement>("EvidenceRefs")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("jsonb")
                    .HasColumnName("evidence_refs")
                    .HasDefaultValueSql("'[]'::jsonb");

                b.Property<int>("Ordinal")
                    .HasColumnType("integer")
                    .HasColumnName("ordinal");

                b.Property<Guid>("RunId")
                    .HasColumnType("uuid")
                    .HasColumnName("run_id");

                b.Property<string>("Stage")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("stage");

                b.Property<string>("Summary")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("summary");

                b.Property<string>("ToolName")
                    .HasColumnType("text")
                    .HasColumnName("tool_name");

                b.HasKey("Id")
                    .HasName("agent_step_pkey");

                b.HasAlternateKey("RunId", "Ordinal")
                    .HasName("agent_step_run_id_ordinal_key");

                b.ToTable("agent_step", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AppUserEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<bool>("Active")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(true)
                    .HasColumnName("active");

                b.Property<string>("DisplayName")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("display_name");

                b.Property<bool>("IsOperator")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false)
                    .HasColumnName("is_operator");

                b.Property<string>("Login")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("login");

                b.Property<string>("PasswordHash")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("password_hash");

                b.HasKey("Id")
                    .HasName("app_user_pkey");

                b.HasAlternateKey("Login")
                    .HasName("app_user_login_key");

                b.ToTable("app_user", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AuthSessionEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<DateTimeOffset>("ExpiresAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("expires_at");

                b.Property<DateTimeOffset?>("RevokedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("revoked_at");

                b.Property<string>("TokenHash")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("token_hash");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("auth_session_pkey");

                b.HasAlternateKey("TokenHash")
                    .HasName("auth_session_token_hash_key");

                b.ToTable("auth_session", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.BackgroundJobEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<int>("Attempts")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("integer")
                    .HasDefaultValue(0)
                    .HasColumnName("attempts");

                b.Property<string>("CompanyId")
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<DateTimeOffset?>("FinishedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("finished_at");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<string>("LastError")
                    .HasColumnType("text")
                    .HasColumnName("last_error");

                b.Property<Guid?>("OwnerUserId")
                    .HasColumnType("uuid")
                    .HasColumnName("owner_user_id");

                b.Property<JsonElement>("Payload")
                    .HasColumnType("jsonb")
                    .HasColumnName("payload");

                b.Property<int>("Priority")
                    .HasColumnType("integer")
                    .HasColumnName("priority");

                b.Property<string>("ResultId")
                    .HasColumnType("text")
                    .HasColumnName("result_id");

                b.Property<string>("ResultType")
                    .HasColumnType("text")
                    .HasColumnName("result_type");

                b.Property<string>("Stage")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("stage");

                b.Property<DateTimeOffset?>("StartedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("started_at");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("status");

                b.HasKey("Id")
                    .HasName("background_job_pkey");

                b.HasIndex("Status", "Priority", "CreatedAt")
                    .HasDatabaseName("job_queue_idx");

                b.ToTable("background_job", null, t =>
                    {
                        t.HasCheckConstraint("background_job_1_check", "status IN ('queued','running','done','failed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ChatMessageEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<JsonElement?>("AnswerJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("answer_json");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<Guid>("InsightId")
                    .HasColumnType("uuid")
                    .HasColumnName("insight_id");

                b.Property<string>("Question")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("question");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("status");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("chat_message_pkey");

                b.HasIndex("UserId", "InsightId", "CreatedAt")
                    .HasDatabaseName("chat_history_idx");

                b.ToTable("chat_message", null, t =>
                    {
                        t.HasCheckConstraint("chat_message_1_check", "status IN ('queued','processing','ready','failed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.CompanyEntity", b =>
            {
                b.Property<string>("Id")
                    .HasColumnType("text")
                    .HasColumnName("id");

                b.Property<string>("City")
                    .HasColumnType("text")
                    .HasColumnName("city");

                b.Property<string>("ClType")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("cl_type");

                b.Property<string>("DatasetVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("dataset_version");

                b.Property<string>("GroupId")
                    .HasColumnType("text")
                    .HasColumnName("group_id");

                b.Property<DateTimeOffset>("ImportedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("imported_at");

                b.Property<string>("MainOkvedCode")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("main_okved_code");

                b.Property<string>("MainOkvedName")
                    .HasColumnType("text")
                    .HasColumnName("main_okved_name");

                b.Property<JsonElement>("QualityFlags")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("jsonb")
                    .HasColumnName("quality_flags")
                    .HasDefaultValueSql("'[]'::jsonb");

                b.Property<JsonElement>("RawData")
                    .HasColumnType("jsonb")
                    .HasColumnName("raw_data");

                b.Property<string>("Region")
                    .HasColumnType("text")
                    .HasColumnName("region");

                b.Property<decimal?>("Revenue")
                    .HasColumnType("numeric(24,2)")
                    .HasColumnName("revenue");

                b.HasKey("Id")
                    .HasName("company_pkey");

                b.HasIndex("GroupId")
                    .HasDatabaseName("company_group_idx");

                b.ToTable("company", null, t =>
                    {
                        t.HasCheckConstraint("company_1_check", "cl_type = 'CLIENT'");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.CompanyProfileEntity", b =>
            {
                b.Property<string>("CompanyId")
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<string>("DataVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("data_version");

                b.Property<JsonElement>("ProfileJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("profile_json");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("updated_at");

                b.HasKey("CompanyId")
                    .HasName("company_profile_pkey");

                b.ToTable("company_profile", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("ContentSha256")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("content_sha256");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<Vector>("Embedding")
                    .HasColumnType("vector(768)")
                    .HasColumnName("embedding");

                b.Property<DateOnly?>("EventDate")
                    .HasColumnType("date")
                    .HasColumnName("event_date");

                b.Property<JsonElement>("ExtractionJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("extraction_json");

                b.Property<string>("Headline")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("headline");

                b.Property<string>("InputVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("input_version");

                b.Property<Guid>("NewsRawId")
                    .HasColumnType("uuid")
                    .HasColumnName("news_raw_id");

                b.Property<DateTimeOffset>("PublishedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("published_at");

                b.Property<NpgsqlTsVector>("SearchText")
                    .IsRequired()
                    .HasColumnType("tsvector")
                    .HasColumnName("search_text");

                b.HasKey("Id")
                    .HasName("event_pkey");

                b.HasAlternateKey("ContentSha256")
                    .HasName("event_content_sha256_key");

                b.HasAlternateKey("NewsRawId")
                    .HasName("event_news_raw_id_key");

                b.HasIndex("PublishedAt")
                    .IsDescending()
                    .HasDatabaseName("event_date_idx");

                b.HasIndex("SearchText")
                    .HasDatabaseName("event_fts_idx");

                NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchText"), "gin");

                b.ToTable("event", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventSourceEntity", b =>
            {
                b.Property<Guid>("EventId")
                    .HasColumnType("uuid")
                    .HasColumnName("event_id");

                b.Property<Guid>("NewsRawId")
                    .HasColumnType("uuid")
                    .HasColumnName("news_raw_id");

                b.HasKey("EventId", "NewsRawId")
                    .HasName("event_source_pkey");

                b.ToTable("event_source", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventTagEntity", b =>
            {
                b.Property<Guid>("EventId")
                    .HasColumnType("uuid")
                    .HasColumnName("event_id");

                b.Property<string>("Kind")
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<string>("Code")
                    .HasColumnType("text")
                    .HasColumnName("code");

                b.HasKey("EventId", "Kind", "Code")
                    .HasName("event_tag_pkey");

                b.HasIndex("Kind", "Code")
                    .HasDatabaseName("event_tag_lookup_idx");

                b.ToTable("event_tag", null, t =>
                    {
                        t.HasCheckConstraint("event_tag_1_check", "kind IN ('factor','okved2','country','region','commodity')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.FactorEntity", b =>
            {
                b.Property<string>("Code")
                    .HasColumnType("text")
                    .HasColumnName("code");

                b.Property<string>("Description")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("description");

                b.Property<JsonElement>("Keywords")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("jsonb")
                    .HasColumnName("keywords")
                    .HasDefaultValueSql("'[]'::jsonb");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("name");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("version");

                b.HasKey("Code")
                    .HasName("factor_pkey");

                b.ToTable("factor", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.FeedSnapshotEntity", b =>
            {
                b.Property<string>("CompanyId")
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<DateTimeOffset>("AsOf")
                    .HasColumnType("timestamptz")
                    .HasColumnName("as_of");

                b.Property<DateTimeOffset?>("CollectionStartedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("collection_started_at");

                b.Property<string>("InputVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("input_version");

                b.Property<JsonElement>("ItemsJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("items_json");

                b.Property<DateTimeOffset>("LastUpdatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("last_updated_at");

                b.Property<DateTimeOffset>("NewsPeriodFrom")
                    .HasColumnType("timestamptz")
                    .HasColumnName("news_period_from");

                b.Property<DateTimeOffset>("NewsPeriodTo")
                    .HasColumnType("timestamptz")
                    .HasColumnName("news_period_to");

                b.Property<string>("RefreshStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("refresh_status");

                b.HasKey("CompanyId")
                    .HasName("feed_snapshot_pkey");

                b.ToTable("feed_snapshot", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.FeedbackEntity", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.Property<string>("CompanyId")
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<Guid>("EventId")
                    .HasColumnType("uuid")
                    .HasColumnName("event_id");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<string>("Reason")
                    .HasColumnType("text")
                    .HasColumnName("reason");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("updated_at");

                b.HasKey("UserId", "CompanyId", "EventId")
                    .HasName("feedback_pkey");

                b.ToTable("feedback", null, t =>
                    {
                        t.HasCheckConstraint("feedback_1_check", "kind IN ('like','dislike','hide')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.GroupProfileEntity", b =>
            {
                b.Property<string>("GroupId")
                    .HasColumnType("text")
                    .HasColumnName("group_id");

                b.Property<string>("DataVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("data_version");

                b.Property<JsonElement>("ProfileJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("profile_json");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("updated_at");

                b.HasKey("GroupId")
                    .HasName("group_profile_pkey");

                b.ToTable("group_profile", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.InsightEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<JsonElement?>("BodyJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("body_json");

                b.Property<string>("CompanyId")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<Guid>("EventId")
                    .HasColumnType("uuid")
                    .HasColumnName("event_id");

                b.Property<string>("InputVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("input_version");

                b.Property<string>("LastError")
                    .HasColumnType("text")
                    .HasColumnName("last_error");

                b.Property<string>("Role")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("role");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("status");

                b.Property<DateTimeOffset>("UpdatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("updated_at");

                b.HasKey("Id")
                    .HasName("insight_pkey");

                b.HasAlternateKey("CompanyId", "EventId", "Role", "InputVersion")
                    .HasName("insight_company_id_event_id_role_input_version_key");

                b.ToTable("insight", null, t =>
                    {
                        t.HasCheckConstraint("insight_1_check", "role IN ('ceo','cfo')");

                        t.HasCheckConstraint("insight_2_check", "status IN ('queued','processing','ready','failed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.LlmCacheEntity", b =>
            {
                b.Property<string>("CacheKey")
                    .HasColumnType("text")
                    .HasColumnName("cache_key");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<string>("Model")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("model");

                b.Property<JsonElement>("ResponseJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("response_json");

                b.Property<string>("SchemaVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("schema_version");

                b.Property<string>("Task")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("task");

                b.HasKey("CacheKey")
                    .HasName("llm_cache_pkey");

                b.ToTable("llm_cache", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.LlmUsageEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<decimal?>("ActualCostRub")
                    .HasColumnType("numeric(18,8)")
                    .HasColumnName("actual_cost_rub");

                b.Property<int>("Attempt")
                    .HasColumnType("integer")
                    .HasColumnName("attempt");

                b.Property<bool>("CacheHit")
                    .HasColumnType("boolean")
                    .HasColumnName("cache_hit");

                b.Property<Guid?>("CardId")
                    .HasColumnType("uuid")
                    .HasColumnName("card_id");

                b.Property<string>("ConfigVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("config_version");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<long>("DurationMs")
                    .HasColumnType("bigint")
                    .HasColumnName("duration_ms");

                b.Property<string>("Error")
                    .HasColumnType("text")
                    .HasColumnName("error");

                b.Property<long>("InputTokens")
                    .HasColumnType("bigint")
                    .HasColumnName("input_tokens");

                b.Property<string>("Model")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("model");

                b.Property<long>("OutputTokens")
                    .HasColumnType("bigint")
                    .HasColumnName("output_tokens");

                b.Property<long?>("ReasoningTokens")
                    .HasColumnType("bigint")
                    .HasColumnName("reasoning_tokens");

                b.Property<string>("RequestKey")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasColumnType("text")
                    .HasDefaultValue("")
                    .HasColumnName("request_key");

                b.Property<decimal>("ReservedCostRub")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("numeric(18,8)")
                    .HasDefaultValue(0m)
                    .HasColumnName("reserved_cost_rub");

                b.Property<Guid>("RunId")
                    .HasColumnType("uuid")
                    .HasColumnName("run_id");

                b.Property<string>("Stage")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("stage");

                b.HasKey("Id")
                    .HasName("llm_usage_pkey");

                b.HasIndex("CreatedAt")
                    .HasDatabaseName("usage_date_idx");

                b.HasIndex("RequestKey", "Stage")
                    .HasDatabaseName("usage_request_stage_idx");

                b.ToTable("llm_usage", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.NewsRawEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("CanonicalUrl")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("canonical_url");

                b.Property<DateTimeOffset>("FetchedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("fetched_at");

                b.Property<string>("LastError")
                    .HasColumnType("text")
                    .HasColumnName("last_error");

                b.Property<DateTimeOffset?>("PublishedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("published_at");

                b.Property<string>("RejectionReason")
                    .HasColumnType("text")
                    .HasColumnName("rejection_reason");

                b.Property<string>("SourceId")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("source_id");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("status");

                b.Property<string>("Text")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("text");

                b.Property<string>("TextSha256")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("text_sha256");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("title");

                b.HasKey("Id")
                    .HasName("news_raw_pkey");

                b.HasAlternateKey("CanonicalUrl")
                    .HasName("news_raw_canonical_url_key");

                b.HasIndex("PublishedAt")
                    .IsDescending()
                    .HasDatabaseName("news_date_idx");

                b.HasIndex("TextSha256")
                    .HasDatabaseName("news_hash_idx");

                b.ToTable("news_raw", null, t =>
                    {
                        t.HasCheckConstraint("news_raw_1_check", "status IN ('queued','processing','accepted','duplicate','rejected','failed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.PaymentEntity", b =>
            {
                b.Property<string>("Payer")
                    .HasColumnType("text")
                    .HasColumnName("payer");

                b.Property<string>("Beneficiary")
                    .HasColumnType("text")
                    .HasColumnName("beneficiary");

                b.Property<string>("DatasetVersion")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("dataset_version");

                b.Property<decimal>("PayAmt")
                    .HasColumnType("numeric(28,2)")
                    .HasColumnName("pay_amt");

                b.Property<long>("PayCnt")
                    .HasColumnType("bigint")
                    .HasColumnName("pay_cnt");

                b.HasKey("Payer", "Beneficiary")
                    .HasName("payment_pkey");

                b.HasIndex("Beneficiary")
                    .HasDatabaseName("payment_beneficiary_idx");

                b.ToTable("payment", null, t =>
                    {
                        t.HasCheckConstraint("payment_1_check", "pay_cnt >= 0");

                        t.HasCheckConstraint("payment_2_check", "payer <> beneficiary");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ProductFactorRuleEntity", b =>
            {
                b.Property<string>("ProductCode")
                    .HasColumnType("text")
                    .HasColumnName("product_code");

                b.Property<string>("FactorCode")
                    .HasColumnType("text")
                    .HasColumnName("factor_code");

                b.Property<string>("Channel")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("channel");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("version");

                b.HasKey("ProductCode", "FactorCode")
                    .HasName("product_factor_rule_pkey");

                b.ToTable("product_factor_rule", (string)null);
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchChunkEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<JsonElement>("ChartData")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("jsonb")
                    .HasColumnName("chart_data")
                    .HasDefaultValueSql("'[]'::jsonb");

                b.Property<Vector>("Embedding")
                    .HasColumnType("vector(768)")
                    .HasColumnName("embedding");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<string>("LocalId")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("local_id");

                b.Property<Guid>("PageId")
                    .HasColumnType("uuid")
                    .HasColumnName("page_id");

                b.Property<string>("ReviewNote")
                    .HasColumnType("text")
                    .HasColumnName("review_note");

                b.Property<string>("ReviewStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("review_status");

                b.Property<NpgsqlTsVector>("SearchText")
                    .IsRequired()
                    .HasColumnType("tsvector")
                    .HasColumnName("search_text");

                b.Property<JsonElement>("TablesJson")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("jsonb")
                    .HasColumnName("tables_json")
                    .HasDefaultValueSql("'[]'::jsonb");

                b.Property<JsonElement>("TagsJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("tags_json");

                b.Property<string>("Text")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("text");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("version");

                b.HasKey("Id")
                    .HasName("research_chunk_pkey");

                b.HasAlternateKey("PageId", "LocalId")
                    .HasName("research_chunk_page_id_local_id_key");

                b.HasIndex("SearchText")
                    .HasDatabaseName("research_fts_idx");

                NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchText"), "gin");

                b.ToTable("research_chunk", null, t =>
                    {
                        t.HasCheckConstraint("research_chunk_1_check", "kind IN ('fact','bank_view','forecast','disclaimer')");

                        t.HasCheckConstraint("research_chunk_2_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchDocEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<string>("FilePath")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("file_path");

                b.Property<string>("OcrStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("ocr_status");

                b.Property<int>("PageCount")
                    .HasColumnType("integer")
                    .HasColumnName("page_count");

                b.Property<DateOnly?>("PublishedOn")
                    .HasColumnType("date")
                    .HasColumnName("published_on");

                b.Property<string>("ReviewStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("review_status");

                b.Property<string>("Sha256")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("sha256");

                b.Property<string>("SourceUrl")
                    .HasColumnType("text")
                    .HasColumnName("source_url");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("title");

                b.Property<Guid?>("UploadedBy")
                    .HasColumnType("uuid")
                    .HasColumnName("uploaded_by");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("version");

                b.HasKey("Id")
                    .HasName("research_doc_pkey");

                b.HasAlternateKey("Sha256")
                    .HasName("research_doc_sha256_key");

                b.ToTable("research_doc", null, t =>
                    {
                        t.HasCheckConstraint("research_doc_1_check", "page_count > 0");

                        t.HasCheckConstraint("research_doc_2_check", "ocr_status IN ('queued','processing','ready','partial','failed')");

                        t.HasCheckConstraint("research_doc_3_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchPageEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<Guid>("DocumentId")
                    .HasColumnType("uuid")
                    .HasColumnName("document_id");

                b.Property<string>("ImagePath")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("image_path");

                b.Property<string>("LastError")
                    .HasColumnType("text")
                    .HasColumnName("last_error");

                b.Property<JsonElement?>("OcrJson")
                    .HasColumnType("jsonb")
                    .HasColumnName("ocr_json");

                b.Property<string>("OcrStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("ocr_status");

                b.Property<int>("PageNo")
                    .HasColumnType("integer")
                    .HasColumnName("page_no");

                b.Property<string>("ReviewStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("review_status");

                b.Property<DateTimeOffset?>("ReviewedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("reviewed_at");

                b.Property<Guid?>("ReviewedBy")
                    .HasColumnType("uuid")
                    .HasColumnName("reviewed_by");

                b.HasKey("Id")
                    .HasName("research_page_pkey");

                b.HasAlternateKey("DocumentId", "PageNo")
                    .HasName("research_page_document_id_page_no_key");

                b.ToTable("research_page", null, t =>
                    {
                        t.HasCheckConstraint("research_page_1_check", "page_no > 0");

                        t.HasCheckConstraint("research_page_2_check", "ocr_status IN ('queued','processing','ready','failed')");

                        t.HasCheckConstraint("research_page_3_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.SectorSensitivityEntity", b =>
            {
                b.Property<string>("Okved2")
                    .HasColumnType("text")
                    .HasColumnName("okved2");

                b.Property<string>("FactorCode")
                    .HasColumnType("text")
                    .HasColumnName("factor_code");

                b.Property<string>("Channel")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("channel");

                b.Property<DateTimeOffset>("ReviewedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("reviewed_at");

                b.Property<string>("ReviewedBy")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("reviewed_by");

                b.Property<string>("Sign")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("sign");

                b.Property<string>("Version")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("version");

                b.Property<decimal>("Weight")
                    .HasColumnType("numeric(6,5)")
                    .HasColumnName("weight");

                b.HasKey("Okved2", "FactorCode")
                    .HasName("sector_sensitivity_pkey");

                b.ToTable("sector_sensitivity", null, t =>
                    {
                        t.HasCheckConstraint("sector_sensitivity_1_check", "weight BETWEEN 0 AND 1");

                        t.HasCheckConstraint("sector_sensitivity_2_check", "sign IN ('positive','negative','mixed')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.SourceEntity", b =>
            {
                b.Property<string>("Id")
                    .HasColumnType("text")
                    .HasColumnName("id");

                b.Property<JsonElement>("AllowedHosts")
                    .HasColumnType("jsonb")
                    .HasColumnName("allowed_hosts");

                b.Property<bool>("Enabled")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("boolean")
                    .HasDefaultValue(false)
                    .HasColumnName("enabled");

                b.Property<string>("FeedUrl")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("feed_url");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<DateTimeOffset?>("LastCheckedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("last_checked_at");

                b.Property<string>("LastError")
                    .HasColumnType("text")
                    .HasColumnName("last_error");

                b.Property<DateTimeOffset?>("LastSuccessAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("last_success_at");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("name");

                b.Property<int>("PollSeconds")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("integer")
                    .HasDefaultValue(120)
                    .HasColumnName("poll_seconds");

                b.Property<decimal>("Reliability")
                    .HasColumnType("numeric(3,2)")
                    .HasColumnName("reliability");

                b.Property<string>("ValidationStatus")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("validation_status");

                b.HasKey("Id")
                    .HasName("source_pkey");

                b.ToTable("source", null, t =>
                    {
                        t.HasCheckConstraint("source_1_check", "kind IN ('official','media')");

                        t.HasCheckConstraint("source_2_check", "poll_seconds >= 60");

                        t.HasCheckConstraint("source_3_check", "reliability BETWEEN 0 AND 1");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.UserActivityEntity", b =>
            {
                b.Property<Guid>("Id")
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                b.Property<string>("ActionId")
                    .HasColumnType("text")
                    .HasColumnName("action_id");

                b.Property<string>("CompanyId")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<DateTimeOffset>("CreatedAt")
                    .HasColumnType("timestamptz")
                    .HasColumnName("created_at");

                b.Property<string>("EvidenceId")
                    .HasColumnType("text")
                    .HasColumnName("evidence_id");

                b.Property<Guid>("InsightId")
                    .HasColumnType("uuid")
                    .HasColumnName("insight_id");

                b.Property<string>("Kind")
                    .IsRequired()
                    .HasColumnType("text")
                    .HasColumnName("kind");

                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.HasKey("Id")
                    .HasName("user_activity_pkey");

                b.HasIndex("UserId", "InsightId", "CreatedAt")
                    .HasDatabaseName("activity_user_insight_idx");

                b.ToTable("user_activity", null, t =>
                    {
                        t.HasCheckConstraint("user_activity_1_check", "kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted')");

                        t.HasCheckConstraint("user_activity_2_check", "(kind = 'source_opened') = (evidence_id IS NOT NULL)");

                        t.HasCheckConstraint("user_activity_3_check", "(kind = 'action_interacted') = (action_id IS NOT NULL)");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.UserCompanyAccessEntity", b =>
            {
                b.Property<Guid>("UserId")
                    .HasColumnType("uuid")
                    .HasColumnName("user_id");

                b.Property<string>("CompanyId")
                    .HasColumnType("text")
                    .HasColumnName("company_id");

                b.Property<string>("Role")
                    .HasColumnType("text")
                    .HasColumnName("role");

                b.HasKey("UserId", "CompanyId", "Role")
                    .HasName("user_company_access_pkey");

                b.ToTable("user_company_access", null, t =>
                    {
                        t.HasCheckConstraint("user_company_access_1_check", "role IN ('ceo','cfo')");
                    });
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AgentRunEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.InsightEntity", null)
                    .WithMany()
                    .HasForeignKey("InsightId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("agent_run_insight_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.BackgroundJobEntity", null)
                    .WithMany()
                    .HasForeignKey("JobId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("agent_run_job_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AgentStepEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.AgentRunEntity", null)
                    .WithMany()
                    .HasForeignKey("RunId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("agent_step_run_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.AuthSessionEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("auth_session_user_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.BackgroundJobEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("background_job_company_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("OwnerUserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("background_job_owner_user_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ChatMessageEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.InsightEntity", null)
                    .WithMany()
                    .HasForeignKey("InsightId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("chat_message_insight_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("chat_message_user_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.CompanyProfileEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("company_profile_company_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.NewsRawEntity", null)
                    .WithMany()
                    .HasForeignKey("NewsRawId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("event_news_raw_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventSourceEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.EventEntity", null)
                    .WithMany()
                    .HasForeignKey("EventId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("event_source_event_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.NewsRawEntity", null)
                    .WithMany()
                    .HasForeignKey("NewsRawId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("event_source_news_raw_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.EventTagEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.EventEntity", null)
                    .WithMany()
                    .HasForeignKey("EventId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("event_tag_event_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.FeedSnapshotEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("feed_snapshot_company_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.FeedbackEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("feedback_company_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.EventEntity", null)
                    .WithMany()
                    .HasForeignKey("EventId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("feedback_event_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("feedback_user_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.InsightEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("insight_company_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.EventEntity", null)
                    .WithMany()
                    .HasForeignKey("EventId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("insight_event_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.LlmUsageEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.InsightEntity", null)
                    .WithMany()
                    .HasForeignKey("CardId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("llm_usage_card_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AgentRunEntity", null)
                    .WithMany()
                    .HasForeignKey("RunId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("llm_usage_run_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.NewsRawEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.SourceEntity", null)
                    .WithMany()
                    .HasForeignKey("SourceId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("news_raw_source_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ProductFactorRuleEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.FactorEntity", null)
                    .WithMany()
                    .HasForeignKey("FactorCode")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("product_factor_rule_factor_code_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchChunkEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.ResearchPageEntity", null)
                    .WithMany()
                    .HasForeignKey("PageId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("research_chunk_page_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchDocEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UploadedBy")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("research_doc_uploaded_by_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.ResearchPageEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.ResearchDocEntity", null)
                    .WithMany()
                    .HasForeignKey("DocumentId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("research_page_document_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("ReviewedBy")
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("research_page_reviewed_by_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.SectorSensitivityEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.FactorEntity", null)
                    .WithMany()
                    .HasForeignKey("FactorCode")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("sector_sensitivity_factor_code_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.UserActivityEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("user_activity_company_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.InsightEntity", null)
                    .WithMany()
                    .HasForeignKey("InsightId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("user_activity_insight_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("user_activity_user_id_fkey");
            });

        modelBuilder.Entity("AlfaNews.Core.Data.Entities.UserCompanyAccessEntity", b =>
            {
                b.HasOne("AlfaNews.Core.Data.Entities.CompanyEntity", null)
                    .WithMany()
                    .HasForeignKey("CompanyId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("user_company_access_company_id_fkey");

                b.HasOne("AlfaNews.Core.Data.Entities.AppUserEntity", null)
                    .WithMany()
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.NoAction)
                    .IsRequired()
                    .HasConstraintName("user_company_access_user_id_fkey");
            });
#pragma warning restore 612, 618
    }
}
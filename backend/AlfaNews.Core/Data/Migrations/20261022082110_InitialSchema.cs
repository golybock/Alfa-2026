using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace AlfaNews.Core.Data.Migrations;

public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "app_user",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                login = table.Column<string>(type: "text", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: false),
                is_operator = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("app_user_pkey", x => x.id);
                table.UniqueConstraint("app_user_login_key", x => x.login);
            });

        migrationBuilder.CreateTable(
            name: "company",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                cl_type = table.Column<string>(type: "text", nullable: false),
                group_id = table.Column<string>(type: "text", nullable: true),
                main_okved_code = table.Column<string>(type: "text", nullable: false),
                main_okved_name = table.Column<string>(type: "text", nullable: true),
                region = table.Column<string>(type: "text", nullable: true),
                city = table.Column<string>(type: "text", nullable: true),
                revenue = table.Column<decimal>(type: "numeric(24,2)", nullable: true),
                raw_data = table.Column<JsonElement>(type: "jsonb", nullable: false),
                dataset_version = table.Column<string>(type: "text", nullable: false),
                quality_flags = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                imported_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("company_pkey", x => x.id);
                table.CheckConstraint("company_1_check", "cl_type = 'CLIENT'");
            });

        migrationBuilder.CreateTable(
            name: "factor",
            columns: table => new
            {
                code = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                keywords = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                version = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("factor_pkey", x => x.code);
            });

        migrationBuilder.CreateTable(
            name: "group_profile",
            columns: table => new
            {
                group_id = table.Column<string>(type: "text", nullable: false),
                data_version = table.Column<string>(type: "text", nullable: false),
                profile_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("group_profile_pkey", x => x.group_id);
            });

        migrationBuilder.CreateTable(
            name: "llm_cache",
            columns: table => new
            {
                cache_key = table.Column<string>(type: "text", nullable: false),
                task = table.Column<string>(type: "text", nullable: false),
                model = table.Column<string>(type: "text", nullable: false),
                schema_version = table.Column<string>(type: "text", nullable: false),
                response_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("llm_cache_pkey", x => x.cache_key);
            });

        migrationBuilder.CreateTable(
            name: "payment",
            columns: table => new
            {
                payer = table.Column<string>(type: "text", nullable: false),
                beneficiary = table.Column<string>(type: "text", nullable: false),
                pay_amt = table.Column<decimal>(type: "numeric(28,2)", nullable: false),
                pay_cnt = table.Column<long>(type: "bigint", nullable: false),
                dataset_version = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("payment_pkey", x => new { x.payer, x.beneficiary });
                table.CheckConstraint("payment_1_check", "pay_cnt >= 0");
                table.CheckConstraint("payment_2_check", "payer <> beneficiary");
            });

        migrationBuilder.CreateTable(
            name: "source",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                feed_url = table.Column<string>(type: "text", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                allowed_hosts = table.Column<JsonElement>(type: "jsonb", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                poll_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 120),
                reliability = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                validation_status = table.Column<string>(type: "text", nullable: false),
                last_checked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                last_success_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("source_pkey", x => x.id);
                table.CheckConstraint("source_1_check", "kind IN ('official','media')");
                table.CheckConstraint("source_2_check", "poll_seconds >= 60");
                table.CheckConstraint("source_3_check", "reliability BETWEEN 0 AND 1");
            });

        migrationBuilder.CreateTable(
            name: "auth_session",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("auth_session_pkey", x => x.id);
                table.UniqueConstraint("auth_session_token_hash_key", x => x.token_hash);
                table.ForeignKey(
                    name: "auth_session_user_id_fkey",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "research_doc",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                sha256 = table.Column<string>(type: "text", nullable: false),
                file_path = table.Column<string>(type: "text", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                source_url = table.Column<string>(type: "text", nullable: true),
                published_on = table.Column<DateOnly>(type: "date", nullable: true),
                page_count = table.Column<int>(type: "integer", nullable: false),
                ocr_status = table.Column<string>(type: "text", nullable: false),
                review_status = table.Column<string>(type: "text", nullable: false),
                version = table.Column<string>(type: "text", nullable: false),
                uploaded_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("research_doc_pkey", x => x.id);
                table.UniqueConstraint("research_doc_sha256_key", x => x.sha256);
                table.CheckConstraint("research_doc_1_check", "page_count > 0");
                table.CheckConstraint("research_doc_2_check", "ocr_status IN ('queued','processing','ready','partial','failed')");
                table.CheckConstraint("research_doc_3_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                table.ForeignKey(
                    name: "research_doc_uploaded_by_fkey",
                    column: x => x.uploaded_by,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "background_job",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                priority = table.Column<int>(type: "integer", nullable: false),
                owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                company_id = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                stage = table.Column<string>(type: "text", nullable: false),
                result_type = table.Column<string>(type: "text", nullable: true),
                result_id = table.Column<string>(type: "text", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("background_job_pkey", x => x.id);
                table.CheckConstraint("background_job_1_check", "status IN ('queued','running','done','failed')");
                table.ForeignKey(
                    name: "background_job_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "background_job_owner_user_id_fkey",
                    column: x => x.owner_user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "company_profile",
            columns: table => new
            {
                company_id = table.Column<string>(type: "text", nullable: false),
                data_version = table.Column<string>(type: "text", nullable: false),
                profile_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("company_profile_pkey", x => x.company_id);
                table.ForeignKey(
                    name: "company_profile_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "feed_snapshot",
            columns: table => new
            {
                company_id = table.Column<string>(type: "text", nullable: false),
                as_of = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                news_period_from = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                news_period_to = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                input_version = table.Column<string>(type: "text", nullable: false),
                items_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                last_updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                collection_started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                refresh_status = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("feed_snapshot_pkey", x => x.company_id);
                table.ForeignKey(
                    name: "feed_snapshot_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "user_company_access",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_id = table.Column<string>(type: "text", nullable: false),
                role = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("user_company_access_pkey", x => new { x.user_id, x.company_id, x.role });
                table.CheckConstraint("user_company_access_1_check", "role IN ('ceo','cfo')");
                table.ForeignKey(
                    name: "user_company_access_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "user_company_access_user_id_fkey",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "product_factor_rule",
            columns: table => new
            {
                product_code = table.Column<string>(type: "text", nullable: false),
                factor_code = table.Column<string>(type: "text", nullable: false),
                channel = table.Column<string>(type: "text", nullable: false),
                version = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("product_factor_rule_pkey", x => new { x.product_code, x.factor_code });
                table.ForeignKey(
                    name: "product_factor_rule_factor_code_fkey",
                    column: x => x.factor_code,
                    principalTable: "factor",
                    principalColumn: "code");
            });

        migrationBuilder.CreateTable(
            name: "sector_sensitivity",
            columns: table => new
            {
                okved2 = table.Column<string>(type: "text", nullable: false),
                factor_code = table.Column<string>(type: "text", nullable: false),
                weight = table.Column<decimal>(type: "numeric(6,5)", nullable: false),
                sign = table.Column<string>(type: "text", nullable: false),
                channel = table.Column<string>(type: "text", nullable: false),
                reviewed_by = table.Column<string>(type: "text", nullable: false),
                reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                version = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("sector_sensitivity_pkey", x => new { x.okved2, x.factor_code });
                table.CheckConstraint("sector_sensitivity_1_check", "weight BETWEEN 0 AND 1");
                table.CheckConstraint("sector_sensitivity_2_check", "sign IN ('positive','negative','mixed')");
                table.ForeignKey(
                    name: "sector_sensitivity_factor_code_fkey",
                    column: x => x.factor_code,
                    principalTable: "factor",
                    principalColumn: "code");
            });

        migrationBuilder.CreateTable(
            name: "news_raw",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source_id = table.Column<string>(type: "text", nullable: false),
                canonical_url = table.Column<string>(type: "text", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                text_sha256 = table.Column<string>(type: "text", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                rejection_reason = table.Column<string>(type: "text", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("news_raw_pkey", x => x.id);
                table.UniqueConstraint("news_raw_canonical_url_key", x => x.canonical_url);
                table.CheckConstraint("news_raw_1_check", "status IN ('queued','processing','accepted','duplicate','rejected','failed')");
                table.ForeignKey(
                    name: "news_raw_source_id_fkey",
                    column: x => x.source_id,
                    principalTable: "source",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "research_page",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                document_id = table.Column<Guid>(type: "uuid", nullable: false),
                page_no = table.Column<int>(type: "integer", nullable: false),
                image_path = table.Column<string>(type: "text", nullable: false),
                ocr_status = table.Column<string>(type: "text", nullable: false),
                ocr_json = table.Column<JsonElement>(type: "jsonb", nullable: true),
                review_status = table.Column<string>(type: "text", nullable: false),
                reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                reviewed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("research_page_pkey", x => x.id);
                table.UniqueConstraint("research_page_document_id_page_no_key", x => new { x.document_id, x.page_no });
                table.CheckConstraint("research_page_1_check", "page_no > 0");
                table.CheckConstraint("research_page_2_check", "ocr_status IN ('queued','processing','ready','failed')");
                table.CheckConstraint("research_page_3_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                table.ForeignKey(
                    name: "research_page_document_id_fkey",
                    column: x => x.document_id,
                    principalTable: "research_doc",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "research_page_reviewed_by_fkey",
                    column: x => x.reviewed_by,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "event",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                news_raw_id = table.Column<Guid>(type: "uuid", nullable: false),
                content_sha256 = table.Column<string>(type: "text", nullable: false),
                headline = table.Column<string>(type: "text", nullable: false),
                event_date = table.Column<DateOnly>(type: "date", nullable: true),
                published_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                extraction_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                input_version = table.Column<string>(type: "text", nullable: false),
                search_text = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false),
                embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("event_pkey", x => x.id);
                table.UniqueConstraint("event_content_sha256_key", x => x.content_sha256);
                table.UniqueConstraint("event_news_raw_id_key", x => x.news_raw_id);
                table.ForeignKey(
                    name: "event_news_raw_id_fkey",
                    column: x => x.news_raw_id,
                    principalTable: "news_raw",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "research_chunk",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                page_id = table.Column<Guid>(type: "uuid", nullable: false),
                local_id = table.Column<string>(type: "text", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                tables_json = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                chart_data = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                tags_json = table.Column<JsonElement>(type: "jsonb", nullable: false),
                review_status = table.Column<string>(type: "text", nullable: false),
                review_note = table.Column<string>(type: "text", nullable: true),
                version = table.Column<string>(type: "text", nullable: false),
                search_text = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false),
                embedding = table.Column<Vector>(type: "vector(768)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("research_chunk_pkey", x => x.id);
                table.UniqueConstraint("research_chunk_page_id_local_id_key", x => new { x.page_id, x.local_id });
                table.CheckConstraint("research_chunk_1_check", "kind IN ('fact','bank_view','forecast','disclaimer')");
                table.CheckConstraint("research_chunk_2_check", "review_status IN ('not_reviewed','partially_reviewed','reviewed')");
                table.ForeignKey(
                    name: "research_chunk_page_id_fkey",
                    column: x => x.page_id,
                    principalTable: "research_page",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "event_source",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                news_raw_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("event_source_pkey", x => new { x.event_id, x.news_raw_id });
                table.ForeignKey(
                    name: "event_source_event_id_fkey",
                    column: x => x.event_id,
                    principalTable: "event",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "event_source_news_raw_id_fkey",
                    column: x => x.news_raw_id,
                    principalTable: "news_raw",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "event_tag",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                code = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("event_tag_pkey", x => new { x.event_id, x.kind, x.code });
                table.CheckConstraint("event_tag_1_check", "kind IN ('factor','okved2','country','region','commodity')");
                table.ForeignKey(
                    name: "event_tag_event_id_fkey",
                    column: x => x.event_id,
                    principalTable: "event",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "feedback",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_id = table.Column<string>(type: "text", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                reason = table.Column<string>(type: "text", nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("feedback_pkey", x => new { x.user_id, x.company_id, x.event_id });
                table.CheckConstraint("feedback_1_check", "kind IN ('like','dislike','hide')");
                table.ForeignKey(
                    name: "feedback_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "feedback_event_id_fkey",
                    column: x => x.event_id,
                    principalTable: "event",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "feedback_user_id_fkey",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "insight",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                company_id = table.Column<string>(type: "text", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "text", nullable: false),
                input_version = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                body_json = table.Column<JsonElement>(type: "jsonb", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("insight_pkey", x => x.id);
                table.UniqueConstraint("insight_company_id_event_id_role_input_version_key", x => new { x.company_id, x.event_id, x.role, x.input_version });
                table.CheckConstraint("insight_1_check", "role IN ('ceo','cfo')");
                table.CheckConstraint("insight_2_check", "status IN ('queued','processing','ready','failed')");
                table.ForeignKey(
                    name: "insight_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "insight_event_id_fkey",
                    column: x => x.event_id,
                    principalTable: "event",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "agent_run",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                insight_id = table.Column<Guid>(type: "uuid", nullable: true),
                input_version = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("agent_run_pkey", x => x.id);
                table.ForeignKey(
                    name: "agent_run_insight_id_fkey",
                    column: x => x.insight_id,
                    principalTable: "insight",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "agent_run_job_id_fkey",
                    column: x => x.job_id,
                    principalTable: "background_job",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "chat_message",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                insight_id = table.Column<Guid>(type: "uuid", nullable: false),
                question = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                answer_json = table.Column<JsonElement>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("chat_message_pkey", x => x.id);
                table.CheckConstraint("chat_message_1_check", "status IN ('queued','processing','ready','failed')");
                table.ForeignKey(
                    name: "chat_message_insight_id_fkey",
                    column: x => x.insight_id,
                    principalTable: "insight",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "chat_message_user_id_fkey",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "user_activity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_id = table.Column<string>(type: "text", nullable: false),
                insight_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                evidence_id = table.Column<string>(type: "text", nullable: true),
                action_id = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("user_activity_pkey", x => x.id);
                table.CheckConstraint("user_activity_1_check", "kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted')");
                table.CheckConstraint("user_activity_2_check", "(kind = 'source_opened') = (evidence_id IS NOT NULL)");
                table.CheckConstraint("user_activity_3_check", "(kind = 'action_interacted') = (action_id IS NOT NULL)");
                table.ForeignKey(
                    name: "user_activity_company_id_fkey",
                    column: x => x.company_id,
                    principalTable: "company",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "user_activity_insight_id_fkey",
                    column: x => x.insight_id,
                    principalTable: "insight",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "user_activity_user_id_fkey",
                    column: x => x.user_id,
                    principalTable: "app_user",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "agent_step",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                stage = table.Column<string>(type: "text", nullable: false),
                tool_name = table.Column<string>(type: "text", nullable: true),
                evidence_refs = table.Column<JsonElement>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                summary = table.Column<string>(type: "text", nullable: false),
                duration_ms = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("agent_step_pkey", x => x.id);
                table.UniqueConstraint("agent_step_run_id_ordinal_key", x => new { x.run_id, x.ordinal });
                table.ForeignKey(
                    name: "agent_step_run_id_fkey",
                    column: x => x.run_id,
                    principalTable: "agent_run",
                    principalColumn: "id");
            });

        migrationBuilder.CreateTable(
            name: "llm_usage",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                card_id = table.Column<Guid>(type: "uuid", nullable: true),
                stage = table.Column<string>(type: "text", nullable: false),
                model = table.Column<string>(type: "text", nullable: false),
                config_version = table.Column<string>(type: "text", nullable: false),
                attempt = table.Column<int>(type: "integer", nullable: false),
                input_tokens = table.Column<long>(type: "bigint", nullable: false),
                output_tokens = table.Column<long>(type: "bigint", nullable: false),
                reasoning_tokens = table.Column<long>(type: "bigint", nullable: true),
                actual_cost_rub = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                duration_ms = table.Column<long>(type: "bigint", nullable: false),
                cache_hit = table.Column<bool>(type: "boolean", nullable: false),
                error = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("llm_usage_pkey", x => x.id);
                table.ForeignKey(
                    name: "llm_usage_card_id_fkey",
                    column: x => x.card_id,
                    principalTable: "insight",
                    principalColumn: "id");
                table.ForeignKey(
                    name: "llm_usage_run_id_fkey",
                    column: x => x.run_id,
                    principalTable: "agent_run",
                    principalColumn: "id");
            });

        migrationBuilder.CreateIndex(
            name: "job_queue_idx",
            table: "background_job",
            columns: new[] { "status", "priority", "created_at" });

        migrationBuilder.CreateIndex(
            name: "chat_history_idx",
            table: "chat_message",
            columns: new[] { "user_id", "insight_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "company_group_idx",
            table: "company",
            column: "group_id");

        migrationBuilder.CreateIndex(
            name: "event_date_idx",
            table: "event",
            column: "published_at",
            descending: new bool[0]);

        migrationBuilder.CreateIndex(
            name: "event_fts_idx",
            table: "event",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin");

        migrationBuilder.CreateIndex(
            name: "event_tag_lookup_idx",
            table: "event_tag",
            columns: new[] { "kind", "code" });

        migrationBuilder.CreateIndex(
            name: "usage_date_idx",
            table: "llm_usage",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "news_date_idx",
            table: "news_raw",
            column: "published_at",
            descending: new bool[0]);

        migrationBuilder.CreateIndex(
            name: "news_hash_idx",
            table: "news_raw",
            column: "text_sha256");

        migrationBuilder.CreateIndex(
            name: "payment_beneficiary_idx",
            table: "payment",
            column: "beneficiary");

        migrationBuilder.CreateIndex(
            name: "research_fts_idx",
            table: "research_chunk",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin");

        migrationBuilder.CreateIndex(
            name: "activity_user_insight_idx",
            table: "user_activity",
            columns: new[] { "user_id", "insight_id", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "agent_step");

        migrationBuilder.DropTable(
            name: "auth_session");

        migrationBuilder.DropTable(
            name: "chat_message");

        migrationBuilder.DropTable(
            name: "company_profile");

        migrationBuilder.DropTable(
            name: "event_source");

        migrationBuilder.DropTable(
            name: "event_tag");

        migrationBuilder.DropTable(
            name: "feed_snapshot");

        migrationBuilder.DropTable(
            name: "feedback");

        migrationBuilder.DropTable(
            name: "group_profile");

        migrationBuilder.DropTable(
            name: "llm_cache");

        migrationBuilder.DropTable(
            name: "llm_usage");

        migrationBuilder.DropTable(
            name: "payment");

        migrationBuilder.DropTable(
            name: "product_factor_rule");

        migrationBuilder.DropTable(
            name: "research_chunk");

        migrationBuilder.DropTable(
            name: "sector_sensitivity");

        migrationBuilder.DropTable(
            name: "user_activity");

        migrationBuilder.DropTable(
            name: "user_company_access");

        migrationBuilder.DropTable(
            name: "agent_run");

        migrationBuilder.DropTable(
            name: "research_page");

        migrationBuilder.DropTable(
            name: "factor");

        migrationBuilder.DropTable(
            name: "insight");

        migrationBuilder.DropTable(
            name: "background_job");

        migrationBuilder.DropTable(
            name: "research_doc");

        migrationBuilder.DropTable(
            name: "event");

        migrationBuilder.DropTable(
            name: "company");

        migrationBuilder.DropTable(
            name: "app_user");

        migrationBuilder.DropTable(
            name: "news_raw");

        migrationBuilder.DropTable(
            name: "source");
    }
}
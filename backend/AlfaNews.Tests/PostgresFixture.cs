using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlfaNews.Core.Access;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AlfaNews.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public string? ConnectionString { get; private set; }
    public Guid UserId { get; } = Guid.NewGuid();
    public Guid OtherUserId { get; } = Guid.NewGuid();
    public Guid OperatorId { get; } = Guid.NewGuid();
    public Guid SessionId { get; } = Guid.NewGuid();
    public Guid OtherSessionId { get; } = Guid.NewGuid();
    public Guid OperatorSessionId { get; } = Guid.NewGuid();
    public Guid InsightId { get; } = Guid.NewGuid();
    public Guid ForeignInsightId { get; } = Guid.NewGuid();
    public Guid DocumentId { get; } = Guid.NewGuid();
    public Guid JobId { get; } = Guid.NewGuid();
    public string Token { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    public string OtherToken { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private string? adminConnection;
    private string? databaseName;

    public UserContext User => new(UserId, SessionId);
    public UserContext Operator => new(OperatorId, OperatorSessionId);

    public AlfaNewsDbContext CreateDb() => new(new DbContextOptionsBuilder<AlfaNewsDbContext>()
        .UseNpgsql(ConnectionString!, options => options.UseVector()).Options);

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("ALFANEWS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnection))
            return;
        databaseName = "alfanews_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", admin))
            await command.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }
            .ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        await SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        if (adminConnection is null || databaseName is null)
            return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync(AlfaNewsDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(1);
        var operatorHash = SessionService.HashToken(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        var tokenHash = SessionService.HashToken(Token);
        var otherHash = SessionService.HashToken(OtherToken);
        var sourceId = "synthetic";
        var newsId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var secondPageId = Guid.NewGuid();
        var json = JsonNode.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Examples", "insight.json")))!;
        json["id"] = InsightId.ToString();
        json["company_id"] = "test_company_a";
        json["event_id"] = eventId.ToString();
        json["role"] = "cfo";
        json["evidence"]!["E2"]!["document_id"] = DocumentId.ToString();
        json["evidence"]!["E2"]!["page"] = 1;
        var body = json.ToJsonString();
        var emptyJson = "{}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
                                                       INSERT INTO company (id, cl_type, main_okved_code, raw_data, dataset_version, imported_at)
                                                       VALUES ('test_company_a', 'CLIENT', '01', {emptyJson}::jsonb, 'synthetic', {now}),
                                                              ('test_company_b', 'CLIENT', '02', {emptyJson}::jsonb, 'synthetic', {now});
                                                       INSERT INTO app_user (id, login, password_hash, display_name, is_operator)
                                                       VALUES ({UserId}, 'synthetic_user', 'disabled', 'Синтетический пользователь', false),
                                                              ({OtherUserId}, 'synthetic_other', 'disabled', 'Другой пользователь', false),
                                                              ({OperatorId}, 'synthetic_operator', 'disabled', 'Оператор', true);
                                                       INSERT INTO auth_session (id, user_id, token_hash, created_at, expires_at)
                                                       VALUES ({SessionId}, {UserId}, {tokenHash}, {now}, {expires}),
                                                              ({OtherSessionId}, {OtherUserId}, {otherHash}, {now}, {expires}),
                                                              ({OperatorSessionId}, {OperatorId}, {operatorHash}, {now}, {expires});
                                                       INSERT INTO user_company_access (user_id, company_id, role)
                                                       VALUES ({UserId}, 'test_company_a', 'cfo'), ({OtherUserId}, 'test_company_a', 'cfo');
                                                       INSERT INTO source (id, name, feed_url, kind, allowed_hosts, reliability, validation_status)
                                                       VALUES ({sourceId}, 'Синтетический источник', 'https://example.org/feed', 'official', '[]', 1, 'test');
                                                       INSERT INTO news_raw (id, source_id, canonical_url, title, text, text_sha256, fetched_at, status)
                                                       VALUES ({newsId}, {sourceId}, 'https://example.org/test', 'Тест', 'Тест', 'synthetic', {now}, 'accepted');
                                                       INSERT INTO event (id, news_raw_id, content_sha256, headline, published_at, extraction_json, input_version, search_text, created_at)
                                                       VALUES ({eventId}, {newsId}, 'synthetic', 'Синтетическое событие', {now}, {emptyJson}::jsonb, 'v1', to_tsvector('russian', 'кредитами'), {now});
                                                       INSERT INTO insight (id, company_id, event_id, role, input_version, status, body_json, created_at, updated_at)
                                                       VALUES ({InsightId}, 'test_company_a', {eventId}, 'cfo', 'v1', 'ready', {body}::jsonb, {now}, {now}),
                                                              ({ForeignInsightId}, 'test_company_b', {eventId}, 'cfo', 'v1', 'ready', {body}::jsonb, {now}, {now});
                                                       INSERT INTO research_doc (id, sha256, file_path, title, page_count, ocr_status, review_status, version, created_at)
                                                       VALUES ({DocumentId}, 'synthetic', 'private/test.pdf', 'Синтетический PDF', 2, 'ready', 'not_reviewed', 'v1', {now});
                                                       INSERT INTO research_page (id, document_id, page_no, image_path, ocr_status, review_status)
                                                       VALUES ({pageId}, {DocumentId}, 1, 'private/1.png', 'ready', 'not_reviewed'),
                                                              ({secondPageId}, {DocumentId}, 2, 'private/2.png', 'ready', 'not_reviewed');
                                                       INSERT INTO background_job (id, kind, payload, status, priority, owner_user_id, company_id, created_at, stage)
                                                       VALUES ({JobId}, 'test', {emptyJson}::jsonb, 'queued', 1, {UserId}, 'test_company_a', {now}, 'test');
                                                       """);
    }
}
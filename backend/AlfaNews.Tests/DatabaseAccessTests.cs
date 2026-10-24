using System.Text.Json;
using AlfaNews.Contracts.Api;
using AlfaNews.Contracts.Domain;
using AlfaNews.Core.Access;
using AlfaNews.Core.Activity;
using AlfaNews.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace AlfaNews.Tests;

public sealed class DatabaseAccessTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    public async Task MigrationMatchesSqlContract()
    {
        await using var db = fixture.CreateDb();
        Assert.False(db.Database.HasPendingModelChanges());
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA expected; SET search_path TO expected, public;");
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database.sql")));
        await db.Database.ExecuteSqlRawAsync("SET search_path TO public;");
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        const string query = """
            SELECT table_name || ':' || column_name || ':' || udt_name || ':' || is_nullable || ':' ||
                coalesce(numeric_precision::text, '') || ':' || coalesce(numeric_scale::text, '') || ':' || coalesce(column_default, '')
            FROM information_schema.columns WHERE table_schema = $1 AND table_name <> '__EFMigrationsHistory'
            ORDER BY table_name, ordinal_position
            """;
        Assert.Equal(await ReadStringsAsync(connection, query, "expected"), await ReadStringsAsync(connection, query, "public"));
        const string constraints = """
            SELECT c.relname || ':' || con.contype::text || ':' || pg_get_constraintdef(con.oid)
            FROM pg_constraint con JOIN pg_class c ON c.oid = con.conrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = $1 AND c.relname <> '__EFMigrationsHistory'
            ORDER BY c.relname, con.contype, pg_get_constraintdef(con.oid)
            """;
        Assert.Equal(await ReadStringsAsync(connection, constraints, "expected"), await ReadStringsAsync(connection, constraints, "public"));
        const string indexes = """
            SELECT tablename || ':' || indexname || ':' || substring(indexdef from 'USING .*')
            FROM pg_indexes WHERE schemaname = $1 AND tablename <> '__EFMigrationsHistory'
                AND indexname NOT IN (SELECT conname FROM pg_constraint WHERE contype IN ('p', 'u'))
            ORDER BY tablename, indexname
            """;
        Assert.Equal(await ReadStringsAsync(connection, indexes, "expected"), await ReadStringsAsync(connection, indexes, "public"));
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA expected CASCADE;");
    }

    [PostgresFact]
    public async Task PaymentAllowsMissingProfilesAndRejectsSelfTransfers()
    {
        await using var db = fixture.CreateDb();
        db.PaymentRows.Add(new PaymentEntity { Payer = "unknown_a", Beneficiary = "unknown_b", PayAmt = 123.45m, PayCnt = 1, DatasetVersion = "test" });
        await db.SaveChangesAsync();
        Assert.Equal(123.45m, (await db.PaymentRows.SingleAsync(row => row.Payer == "unknown_a")).PayAmt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO payment VALUES ('self', 'self', 1, 1, 'test')"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [PostgresFact]
    public async Task JsonFtsAndVectorsRoundTrip()
    {
        await using var db = fixture.CreateDb();
        Assert.True(await db.EventRows.AnyAsync(row => row.SearchText.Matches(EF.Functions.PlainToTsQuery("russian", "кредиты"))));
        var item = await db.EventRows.SingleAsync();
        item.Embedding = new Vector(new float[768]);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        item = await db.EventRows.SingleAsync();
        Assert.Equal(768, item.Embedding!.ToArray().Length);
        Assert.Equal(JsonValueKind.Object, item.ExtractionJson.ValueKind);
        item.Embedding = new Vector(new float[2]);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task CompanyRoleAndOperatorChecksDoNotGrantExtraAccess()
    {
        await using var db = fixture.CreateDb();
        var access = new AccessService(db, TimeProvider.System);
        await access.RequireCompanyAccessAsync(fixture.User, "test_company_a", Role.Cfo, CancellationToken.None);
        await AssertDeniedAsync(403, () => access.RequireCompanyAccessAsync(fixture.User, "test_company_b", Role.Cfo, CancellationToken.None));
        await AssertDeniedAsync(403, () => access.RequireCompanyAccessAsync(fixture.User, "test_company_a", Role.Ceo, CancellationToken.None));
        await AssertDeniedAsync(403, () => access.RequireOperatorAsync(fixture.User, CancellationToken.None));
        await access.RequireOperatorAsync(fixture.Operator, CancellationToken.None);
        await AssertDeniedAsync(403, () => access.RequireCompanyAccessAsync(fixture.Operator, "test_company_a", Role.Cfo, CancellationToken.None));
    }

    [PostgresFact]
    public async Task EvidencePdfAndJobsStayWithinTheirAccessScope()
    {
        await using var db = fixture.CreateDb();
        var access = new AccessService(db, TimeProvider.System);
        await access.RequireEvidenceAsync(fixture.User, fixture.InsightId, "E2", CancellationToken.None);
        await AssertDeniedAsync(404, () => access.RequireEvidenceAsync(fixture.User, fixture.InsightId, "foreign", CancellationToken.None));
        await AssertDeniedAsync(403, () => access.RequireInsightAsync(fixture.User, fixture.ForeignInsightId, CancellationToken.None));
        await access.RequireResearchPageAsync(fixture.User, fixture.DocumentId, 1, CancellationToken.None);
        // Материалы в демонстрации открыты всем вошедшим, поэтому и страницы PDF без ссылки из своей карточки.
        await access.RequireResearchPageAsync(fixture.User, fixture.DocumentId, 2, CancellationToken.None);
        await access.RequireResearchPageAsync(fixture.Operator, fixture.DocumentId, 2, CancellationToken.None);
        await access.RequireJobAsync(fixture.User, fixture.JobId, CancellationToken.None);
        await access.RequireJobAsync(fixture.Operator, fixture.JobId, CancellationToken.None);
        await AssertDeniedAsync(403, () => access.RequireJobAsync(new UserContext(fixture.OtherUserId, fixture.OtherSessionId), fixture.JobId, CancellationToken.None));
    }

    [PostgresFact]
    public async Task RevokedExpiredAndInactiveSessionsAreRejected()
    {
        await using var db = fixture.CreateDb();
        var sessions = new SessionService(db, TimeProvider.System);
        Assert.Equal(fixture.User, await sessions.ResolveAsync(fixture.Token, CancellationToken.None));
        Assert.Null(await sessions.ResolveAsync("invalid", CancellationToken.None));
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE auth_session SET revoked_at = {DateTimeOffset.UtcNow} WHERE id = {fixture.SessionId}");
        Assert.Null(await sessions.ResolveAsync(fixture.Token, CancellationToken.None));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE auth_session SET revoked_at = NULL, expires_at = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE id = {fixture.SessionId}");
        Assert.Null(await sessions.ResolveAsync(fixture.Token, CancellationToken.None));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE auth_session SET expires_at = {DateTimeOffset.UtcNow.AddHours(1)} WHERE id = {fixture.SessionId}; UPDATE app_user SET active = false WHERE id = {fixture.UserId}");
        Assert.Null(await sessions.ResolveAsync(fixture.Token, CancellationToken.None));
        var access = new AccessService(db, TimeProvider.System);
        await AssertDeniedAsync(401, () => access.RequireUserAsync(fixture.User, CancellationToken.None));
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    public async Task ActivityValidatesReferencesAndDeduplicatesDelivery()
    {
        await using var db = fixture.CreateDb();
        var activities = new UserActivityService(db, new AccessService(db, TimeProvider.System), TimeProvider.System);
        var request = new UserActivityRequest(Guid.NewGuid().ToString(), UserActivityType.SourceOpened, "E2", null);
        await activities.RecordAsync(fixture.User, fixture.InsightId, request, CancellationToken.None);
        await activities.RecordAsync(fixture.User, fixture.InsightId, request, CancellationToken.None);
        var row = await db.UserActivityRows.SingleAsync(item => item.Id == Guid.Parse(request.EventId));
        Assert.Equal(fixture.UserId, row.UserId);
        Assert.Equal("test_company_a", row.CompanyId);
        Assert.True(row.CreatedAt <= DateTimeOffset.UtcNow);
        await AssertDeniedAsync(409, () => activities.RecordAsync(fixture.User, fixture.InsightId,
            request with { Type = UserActivityType.InsightOpened, EvidenceId = null }, CancellationToken.None));
        await AssertDeniedAsync(400, () => activities.RecordAsync(fixture.User, fixture.InsightId,
            request with { EvidenceId = "foreign" }, CancellationToken.None));
        await AssertDeniedAsync(400, () => activities.RecordAsync(fixture.User, fixture.InsightId,
            request with { Type = UserActivityType.ChatQuestionAccepted, EvidenceId = null }, CancellationToken.None));
        await AssertDeniedAsync(400, () => activities.RecordAsync(fixture.User, fixture.InsightId,
            request with { Type = UserActivityType.ActionInteracted, EvidenceId = null, ActionId = "foreign" }, CancellationToken.None));
        await AssertDeniedAsync(403, () => activities.RecordAsync(fixture.User, fixture.ForeignInsightId, request, CancellationToken.None));
    }

    [PostgresFact]
    public async Task ChatHistoryIsPrivateAndAcceptedQuestionIsCountedOnce()
    {
        await using var db = fixture.CreateDb();
        var messageId = Guid.NewGuid();
        db.ChatMessageRows.AddRange(
            new ChatMessageEntity { Id = messageId, UserId = fixture.UserId, InsightId = fixture.InsightId, Question = "Тест", Status = "queued", CreatedAt = DateTimeOffset.UtcNow },
            new ChatMessageEntity { Id = Guid.NewGuid(), UserId = fixture.OtherUserId, InsightId = fixture.InsightId, Question = "Чужой вопрос", Status = "queued", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var access = new AccessService(db, TimeProvider.System);
        var history = await access.ReadChatHistoryAsync(fixture.User, fixture.InsightId, CancellationToken.None);
        Assert.Single(history);
        Assert.Equal(messageId, history[0].Id);
        var activities = new UserActivityService(db, access, TimeProvider.System);
        await activities.RecordAcceptedChatAsync(fixture.User, messageId, CancellationToken.None);
        await activities.RecordAcceptedChatAsync(fixture.User, messageId, CancellationToken.None);
        Assert.Equal(1, await db.UserActivityRows.CountAsync(row => row.Id == messageId));
    }

    private static async Task AssertDeniedAsync(int status, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<AccessException>(action);
        Assert.Equal(status, error.StatusCode);
    }

    private static async Task<string[]> ReadStringsAsync(NpgsqlConnection connection, string sql, string schema)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(schema);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0).Replace("expected.", ""));
        return rows.ToArray();
    }
}
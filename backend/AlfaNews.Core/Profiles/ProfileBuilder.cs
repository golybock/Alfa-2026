using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Domain;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace AlfaNews.Core.Profiles;

public sealed class ProfileBuilder(AlfaNewsDbContext db)
{
    public const string AlgorithmVersion = "profiles-v2";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ProfileBuildResult> BuildAsync(RebuildProfilesJob job, string reportPath, bool onlyIfCurrent,
        CancellationToken cancellationToken, Guid? backgroundJobId = null)
    {
        if (string.IsNullOrWhiteSpace(job.DatasetVersion))
            throw new ArgumentException("Не задана версия исходных данных.", nameof(job));
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new InvalidOperationException("Профили нельзя строить до применения всех миграций БД.");

        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await ExecuteAsync(connection, "SELECT pg_advisory_xact_lock(43003); LOCK TABLE company, payment IN SHARE MODE;", cancellationToken);
        await using (var source = new NpgsqlCommand("""
            CREATE TEMP TABLE profile_company_source ON COMMIT DROP AS
            SELECT c.*, CASE WHEN main_okved_code ~ '^[0-9]{2}([.]|$)'
                THEN left(main_okved_code, 2) ELSE NULL END AS okved2
            FROM company c WHERE dataset_version = @version AND cl_type = 'CLIENT';
            CREATE UNIQUE INDEX ON profile_company_source (id);
            ANALYZE profile_company_source;
            """, connection))
        {
            source.Parameters.AddWithValue("version", job.DatasetVersion);
            await source.ExecuteNonQueryAsync(cancellationToken);
        }
        await ValidateSourceAsync(connection, job.DatasetVersion, cancellationToken);
        var dataVersion = await ReadDataVersionAsync(connection, job.DatasetVersion, cancellationToken);
        if (onlyIfCurrent && await ProfilesAreCurrentAsync(connection, job.DatasetVersion, dataVersion, cancellationToken)
            && await ReadCurrentReportAsync(reportPath, dataVersion, cancellationToken) is { } current)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ProfileBuildResult(true, current);
        }

        var exposures = await ReadExposuresAsync(connection, cancellationToken);
        var rows = await db.CompanyRows.AsNoTracking().Where(company => company.DatasetVersion == job.DatasetVersion && company.ClType == "CLIENT")
            .OrderBy(company => company.Id).ToListAsync(cancellationToken);
        var empty = new PaymentExposure(0, 0, 0, new SortedDictionary<string, decimal>(StringComparer.Ordinal));
        var companies = rows.Select(company => CompanyProfileMapper.Map(company,
            exposures.GetValueOrDefault((company.Id, "incoming"), empty),
            exposures.GetValueOrDefault((company.Id, "outgoing"), empty), dataVersion)).ToArray();
        var groups = companies.Where(company => company.GroupId is not null)
            .GroupBy(company => company.GroupId!, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var known = group.Where(company => company.Revenue.HasValue).Select(company => company.Revenue!.Value).ToArray();
                return new GroupProfile(group.Key, group.Select(company => company.CompanyId).Order(StringComparer.Ordinal).ToArray(),
                    known.Length == 0 ? null : known.Sum(), known.Length, dataVersion);
            }).ToArray();
        var report = ProfileReportBuilder.Create(job.DatasetVersion, dataVersion, AlgorithmVersion, companies, groups);
        await SaveAsync(connection, job, dataVersion, companies, groups, cancellationToken, backgroundJobId);
        await WriteReportAsync(reportPath, report, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ProfileBuildResult(false, report);
    }

    private static async Task ValidateSourceAsync(NpgsqlConnection connection, string version, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM profile_company_source)
                OR EXISTS (SELECT 1 FROM background_job WHERE kind = 'ImportCsv' AND status = 'done'
                    AND payload->>'dataset_version' = @version),
                EXISTS (SELECT 1 FROM payment p WHERE p.dataset_version <> @version
                    AND (EXISTS (SELECT 1 FROM profile_company_source c WHERE c.id = p.payer)
                        OR EXISTS (SELECT 1 FROM profile_company_source c WHERE c.id = p.beneficiary)))
            """, connection);
        command.Parameters.AddWithValue("version", version);
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        if (!reader.GetBoolean(0))
            throw new InvalidOperationException("Нет очищенных компаний или завершённого импорта указанной версии.");
        if (reader.GetBoolean(1))
            throw new InvalidOperationException("Платежи компаний относятся к другой версии данных; завершите согласованный импорт.");
    }

    private static async Task<string> ReadDataVersionAsync(NpgsqlConnection connection, string version, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT md5(coalesce(string_agg(md5((to_jsonb(c) - 'imported_at')::text), '' ORDER BY id), ''))
                    FROM profile_company_source c),
                (SELECT md5(coalesce(string_agg(md5(ROW(p.payer,p.beneficiary,p.pay_amt,p.pay_cnt,p.dataset_version)::text), ''
                    ORDER BY p.payer,p.beneficiary), '')) FROM payment p
                    WHERE EXISTS (SELECT 1 FROM profile_company_source c WHERE c.id = p.payer)
                        OR EXISTS (SELECT 1 FROM profile_company_source c WHERE c.id = p.beneficiary))
            """, connection) { CommandTimeout = 300 };
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        var fingerprint = string.Join('\n', AlgorithmVersion, CountryNames.Version, reader.GetString(0), reader.GetString(1));
        return version + ":profiles:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    private static async Task<bool> ProfilesAreCurrentAsync(NpgsqlConnection connection, string datasetVersion, string dataVersion, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM background_job WHERE kind = 'RebuildProfiles' AND status = 'done'
                    AND stage = 'profiles_built_v1' AND result_id = @version)
                AND NOT EXISTS (SELECT 1 FROM profile_company_source c LEFT JOIN company_profile p ON p.company_id = c.id
                    WHERE p.company_id IS NULL OR p.data_version <> @version
                        OR p.profile_json->>'data_version' IS DISTINCT FROM @version)
                AND NOT EXISTS (SELECT DISTINCT c.group_id FROM profile_company_source c
                    LEFT JOIN group_profile p ON p.group_id = c.group_id
                    WHERE c.group_id IS NOT NULL AND (p.group_id IS NULL OR p.data_version <> @version
                        OR p.profile_json->>'data_version' IS DISTINCT FROM @version))
                AND NOT EXISTS (SELECT 1 FROM company_profile p
                    WHERE left(p.data_version, length(@prefix)) = @prefix
                        AND NOT EXISTS (SELECT 1 FROM profile_company_source c WHERE c.id = p.company_id))
                AND NOT EXISTS (SELECT 1 FROM group_profile p
                    WHERE left(p.data_version, length(@prefix)) = @prefix
                        AND NOT EXISTS (SELECT 1 FROM profile_company_source c WHERE c.group_id = p.group_id))
            """, connection);
        command.Parameters.AddWithValue("version", dataVersion);
        command.Parameters.AddWithValue("prefix", datasetVersion + ":profiles:");
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }

    private static async Task<Dictionary<(string CompanyId, string Direction), PaymentExposure>> ReadExposuresAsync(
        NpgsqlConnection connection, CancellationToken token)
    {
        await using var command = new NpgsqlCommand("""
            WITH directed AS (
                SELECT c.id, 'outgoing' AS direction, counterparty.okved2 AS sector, p.pay_amt
                FROM payment p JOIN profile_company_source c ON c.id = p.payer
                LEFT JOIN profile_company_source counterparty ON counterparty.id = p.beneficiary
                WHERE p.payer <> p.beneficiary AND p.pay_amt >= 0
                UNION ALL
                SELECT c.id, 'incoming', counterparty.okved2, p.pay_amt
                FROM payment p JOIN profile_company_source c ON c.id = p.beneficiary
                LEFT JOIN profile_company_source counterparty ON counterparty.id = p.payer
                WHERE p.payer <> p.beneficiary AND p.pay_amt >= 0
            )
            SELECT id, direction, sector, sum(pay_amt) FROM directed
            GROUP BY id, direction, sector ORDER BY id, direction, sector
            """, connection) { CommandTimeout = 300 };
        var rows = new List<ExposureRow>();
        await using (var reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
                rows.Add(new ExposureRow(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetDecimal(3)));
        }
        return rows.GroupBy(row => (row.CompanyId, row.Direction)).ToDictionary(group => group.Key, group =>
        {
            var total = group.Sum(row => row.Amount);
            var known = group.Where(row => row.Sector is not null).Sum(row => row.Amount);
            var shares = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            if (total > 0)
                foreach (var row in group.Where(row => row.Sector is not null && row.Amount > 0))
                    shares.Add(row.Sector!, row.Amount / total);
            return new PaymentExposure(total, known, total == 0 ? 0 : known / total, shares);
        });
    }

    private static async Task SaveAsync(NpgsqlConnection connection, RebuildProfilesJob job, string dataVersion,
        IReadOnlyList<CompanyProfile> companies, IReadOnlyList<GroupProfile> groups, CancellationToken token, Guid? backgroundJobId)
    {
        await ExecuteAsync(connection, """
            CREATE TEMP TABLE profile_company_output (id text, body jsonb) ON COMMIT DROP;
            CREATE TEMP TABLE profile_group_output (id text, body jsonb) ON COMMIT DROP;
            """, token);
        await using (var copy = await connection.BeginBinaryImportAsync("COPY profile_company_output FROM STDIN (FORMAT BINARY)", token))
        {
            foreach (var company in companies)
            {
                await copy.StartRowAsync(token);
                await copy.WriteAsync(company.CompanyId, NpgsqlDbType.Text, token);
                await copy.WriteAsync(JsonSerializer.Serialize(company, JsonOptions), NpgsqlDbType.Jsonb, token);
            }
            await copy.CompleteAsync(token);
        }
        await using (var copy = await connection.BeginBinaryImportAsync("COPY profile_group_output FROM STDIN (FORMAT BINARY)", token))
        {
            foreach (var group in groups)
            {
                await copy.StartRowAsync(token);
                await copy.WriteAsync(group.GroupId, NpgsqlDbType.Text, token);
                await copy.WriteAsync(JsonSerializer.Serialize(group, JsonOptions), NpgsqlDbType.Jsonb, token);
            }
            await copy.CompleteAsync(token);
        }
        await using var save = new NpgsqlCommand("""
            DELETE FROM company_profile WHERE left(data_version, length(@prefix)) = @prefix
                AND company_id NOT IN (SELECT id FROM profile_company_output);
            DELETE FROM group_profile WHERE (left(data_version, length(@prefix)) = @prefix
                    AND group_id NOT IN (SELECT id FROM profile_group_output))
                OR NOT EXISTS (SELECT 1 FROM company c WHERE c.group_id = group_profile.group_id);
            INSERT INTO company_profile (company_id, data_version, profile_json, updated_at)
            SELECT id, @version, body, now() FROM profile_company_output
            ON CONFLICT (company_id) DO UPDATE SET data_version = EXCLUDED.data_version,
                profile_json = EXCLUDED.profile_json, updated_at = EXCLUDED.updated_at
            WHERE company_profile.data_version IS DISTINCT FROM EXCLUDED.data_version
                OR company_profile.profile_json IS DISTINCT FROM EXCLUDED.profile_json;
            INSERT INTO group_profile (group_id, data_version, profile_json, updated_at)
            SELECT id, @version, body, now() FROM profile_group_output
            ON CONFLICT (group_id) DO UPDATE SET data_version = EXCLUDED.data_version,
                profile_json = EXCLUDED.profile_json, updated_at = EXCLUDED.updated_at
            WHERE group_profile.data_version IS DISTINCT FROM EXCLUDED.data_version
                OR group_profile.profile_json IS DISTINCT FROM EXCLUDED.profile_json;
            INSERT INTO background_job (id, kind, payload, status, priority, created_at, started_at,
                finished_at, attempts, stage, result_type, result_id)
            VALUES (@id, 'RebuildProfiles', @payload, 'done', 0, now(), now(), now(), 1,
                'profiles_built_v1', 'profile_version', @version)
            ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status, finished_at = EXCLUDED.finished_at,
                stage = EXCLUDED.stage, result_type = EXCLUDED.result_type, result_id = EXCLUDED.result_id;
            """, connection) { CommandTimeout = 300 };
        save.Parameters.AddWithValue("prefix", job.DatasetVersion + ":profiles:");
        save.Parameters.AddWithValue("version", dataVersion);
        save.Parameters.AddWithValue("id", backgroundJobId ?? Guid.NewGuid());
        save.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(job, JsonOptions));
        await save.ExecuteNonQueryAsync(token);
    }

    private static async Task<ProfileBuildReport?> ReadCurrentReportAsync(string path, string dataVersion, CancellationToken token)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            var text = await File.ReadAllTextAsync(path, token);
            var report = JsonSerializer.Deserialize<ProfileBuildReport>(text, JsonOptions);
            return report?.DataVersion == dataVersion && text == JsonSerializer.Serialize(report, JsonOptions) ? report : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteReportAsync(string path, ProfileBuildReport report, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report, JsonOptions), token);
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(token);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        ContractJson.Configure(options);
        return options;
    }

    private sealed record ExposureRow(string CompanyId, string Direction, string? Sector, decimal Amount);
}
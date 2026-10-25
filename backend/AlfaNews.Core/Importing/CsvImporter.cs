using System.Globalization;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using AlfaNews.Contracts;
using AlfaNews.Contracts.Processing;
using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace AlfaNews.Core.Importing;

public sealed class CsvImporter(AlfaNewsDbContext db)
{
    public static readonly string[] CompanyHeaders =
    [
        "SYNTHETIC_U", "ORG_TYPE", "OKFS_NAME", "COMPANY_AGE_YEARS_GRADATION", "ECO_MARK_LABEL",
        "MAINOKVED_CODE", "MAINOKVED_NAME", "NEGATIVEMARK", "AMOUNT_WORKERS_FNS_GRADATION",
        "AMOUNT_WORKERS_SPARK_GRADATION", "CL_TYPE", "CL_PRODUCT_RKO_MARK", "CL_PRODUCT_GAR_MARK",
        "CL_PRODUCT_LOA_MARK", "CL_PRODUCT_LEA_MARK", "CL_PRODUCT_DEP_MARK", "CL_PRODUCT_ZPP_MARK",
        "FTS_FLAG", "FTS_COUNTRY_NAME", "FACTORING_MARK", "REVENUE_COMPANY", "GROUP_ID", "CITY", "REGION"
    ];

    private static readonly string[] PaymentHeaders = ["BENEFICIARY", "PAYER", "PAY_AMT", "PAY_CNT"];
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private const decimal AmountLimit = 100000000000000000000000000m;
    private const decimal RevenueLimit = 10000000000000000000000m;
    private const string CompletedStage = "csv_imported_known_payments_v1";

    public async Task<CsvImportResult> ImportAsync(ImportCsvJob job, string reportPath, bool onlyIfMissing,
        CancellationToken cancellationToken, Guid? backgroundJobId = null)
    {
        if (string.IsNullOrWhiteSpace(job.DatasetVersion))
            throw new ArgumentException("Не задана версия CSV-пакета.", nameof(job));
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new InvalidOperationException("Импорт CSV запрещён до успешного применения всех миграций БД.");

        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await ExecuteAsync(connection, "SELECT pg_advisory_xact_lock(43003)", cancellationToken);
        var payload = JsonSerializer.Serialize(job, JsonOptions);
        await using var previous = new NpgsqlCommand("""
                                                     SELECT result_id, stage FROM background_job
                                                     WHERE kind = 'ImportCsv' AND status = 'done' AND payload->>'dataset_version' = $1
                                                     ORDER BY finished_at DESC LIMIT 1
                                                     """, connection);
        previous.Parameters.AddWithValue(job.DatasetVersion);
        string? fingerprint = null;
        var currentFilterApplied = false;
        await using (var reader = await previous.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                fingerprint = reader.IsDBNull(0) ? null : reader.GetString(0);
                currentFilterApplied = reader.GetString(1) == CompletedStage;
            }
        }

        if (onlyIfMissing && fingerprint is not null && currentFilterApplied)
        {
            await transaction.CommitAsync(cancellationToken);
            return new CsvImportResult(true, null);
        }

        await using var companies = OpenInput(job.CompanyFile);
        await using var payments = OpenInput(job.PaymentFile);
        var companyHash = await HashAsync(companies, cancellationToken);
        var paymentHash = await HashAsync(payments, cancellationToken);
        var packageHash = companyHash + ":" + paymentHash;
        if (fingerprint is not null && fingerprint != packageHash)
            throw new InvalidOperationException(
                "Эта версия пакета уже импортирована с другими хэшами. Задайте новую версию.");

        await ExecuteAsync(connection, """
                                       CREATE TEMP TABLE csv_company (
                                           row_no bigint, id text, cl_type text, okved text, revenue numeric,
                                           raw_data jsonb, rejection text, quality_flags jsonb) ON COMMIT DROP;
                                       CREATE TEMP TABLE csv_payment (
                                           row_no bigint, payer text, beneficiary text, pay_amt numeric, pay_cnt bigint,
                                           rejection text) ON COMMIT DROP;
                                       """, cancellationToken);
        await CopyCompaniesAsync(connection, companies, cancellationToken);
        await CopyPaymentsAsync(connection, payments, cancellationToken);
        await ExecuteAsync(connection, """
                                       CREATE TEMP TABLE csv_company_clean ON COMMIT DROP AS
                                       WITH conflicts AS (
                                           SELECT id FROM csv_company WHERE id <> '' AND rejection IS DISTINCT FROM 'invalid_column_count'
                                           GROUP BY id HAVING count(DISTINCT raw_data) > 1
                                       ), numbered AS (
                                           SELECT *, row_number() OVER (PARTITION BY id, raw_data ORDER BY row_no) AS duplicate_no
                                           FROM csv_company
                                       )
                                       SELECT n.*, CASE
                                           WHEN rejection = 'invalid_column_count' THEN rejection
                                           WHEN id = '' THEN 'missing_id'
                                           WHEN id IN (SELECT id FROM conflicts) THEN 'conflicting_id'
                                           WHEN cl_type <> 'CLIENT' THEN 'not_client'
                                           WHEN duplicate_no > 1 THEN 'identical_duplicate'
                                           ELSE rejection END AS reason
                                       FROM numbered n;
                                       UPDATE csv_company_clean SET reason = 'group_revenue_below_threshold'
                                       WHERE reason IS NULL AND nullif(btrim(raw_data->>'GROUP_ID'), '') IN (
                                           SELECT nullif(btrim(raw_data->>'GROUP_ID'), '')
                                           FROM csv_company_clean
                                           WHERE reason IS NULL AND nullif(btrim(raw_data->>'GROUP_ID'), '') IS NOT NULL
                                           GROUP BY nullif(btrim(raw_data->>'GROUP_ID'), '')
                                           HAVING sum(revenue) < 15000000000
                                       );
                                       CREATE INDEX ON csv_company_clean (id) WHERE reason IS NULL;
                                       ANALYZE csv_company_clean;
                                       CREATE TEMP TABLE csv_company_ids ON COMMIT DROP AS
                                       SELECT c.id FROM company c
                                       WHERE NOT EXISTS (SELECT 1 FROM csv_company_clean imported
                                           WHERE imported.id = c.id AND imported.reason = 'group_revenue_below_threshold')
                                       UNION SELECT id FROM csv_company_clean WHERE reason IS NULL;
                                       CREATE UNIQUE INDEX ON csv_company_ids (id);
                                       ANALYZE csv_company_ids;
                                       CREATE TEMP TABLE csv_payment_groups ON COMMIT DROP AS
                                       SELECT payer, beneficiary, sum(pay_amt) AS pay_amt, sum(pay_cnt) AS pay_cnt, count(*) AS source_rows
                                       FROM csv_payment WHERE rejection IS NULL GROUP BY payer, beneficiary;
                                       ALTER TABLE csv_payment_groups ADD COLUMN rejection text;
                                       UPDATE csv_payment_groups p SET rejection = CASE
                                           WHEN pay_amt >= 100000000000000000000000000 OR pay_cnt > 9223372036854775807
                                               THEN 'aggregate_overflow'
                                           WHEN NOT EXISTS (SELECT 1 FROM csv_company_ids c WHERE c.id = p.payer)
                                               AND NOT EXISTS (SELECT 1 FROM csv_company_ids c WHERE c.id = p.beneficiary)
                                               THEN 'no_known_participant'
                                           ELSE NULL END;
                                       """, cancellationToken);

        var companyReport = await ReadCompanyReportAsync(connection, cancellationToken);
        var paymentReport = await ReadPaymentReportAsync(connection, cancellationToken);
        var report = new CsvImportReport(job.DatasetVersion, companyHash, paymentHash, companyReport, paymentReport);
        await using var save = new NpgsqlCommand("""
                                                 DELETE FROM company_profile p USING csv_company_clean imported
                                                 WHERE p.company_id = imported.id AND imported.reason = 'group_revenue_below_threshold';
                                                 DELETE FROM company c USING csv_company_clean imported
                                                 WHERE c.id = imported.id AND imported.reason = 'group_revenue_below_threshold';
                                                 INSERT INTO company (id, cl_type, group_id, main_okved_code, main_okved_name, region, city,
                                                     revenue, raw_data, dataset_version, quality_flags, imported_at)
                                                 SELECT id, cl_type, nullif(btrim(raw_data->>'GROUP_ID'), ''), okved,
                                                     nullif(btrim(raw_data->>'MAINOKVED_NAME'), ''), nullif(btrim(raw_data->>'REGION'), ''),
                                                     nullif(btrim(raw_data->>'CITY'), ''), revenue, raw_data, @version, quality_flags, now()
                                                 FROM csv_company_clean WHERE reason IS NULL
                                                 ON CONFLICT (id) DO UPDATE SET cl_type = EXCLUDED.cl_type, group_id = EXCLUDED.group_id,
                                                     main_okved_code = EXCLUDED.main_okved_code, main_okved_name = EXCLUDED.main_okved_name,
                                                     region = EXCLUDED.region, city = EXCLUDED.city, revenue = EXCLUDED.revenue,
                                                     raw_data = EXCLUDED.raw_data, dataset_version = EXCLUDED.dataset_version,
                                                     quality_flags = EXCLUDED.quality_flags, imported_at = EXCLUDED.imported_at;
                                                 DELETE FROM payment p
                                                 WHERE NOT EXISTS (SELECT 1 FROM company c WHERE c.id = p.payer)
                                                     AND NOT EXISTS (SELECT 1 FROM company c WHERE c.id = p.beneficiary);
                                                 INSERT INTO payment (payer, beneficiary, pay_amt, pay_cnt, dataset_version)
                                                 SELECT payer, beneficiary, pay_amt, pay_cnt::bigint, @version
                                                 FROM csv_payment_groups WHERE rejection IS NULL
                                                 ON CONFLICT (payer, beneficiary) DO UPDATE SET pay_amt = EXCLUDED.pay_amt,
                                                     pay_cnt = EXCLUDED.pay_cnt, dataset_version = EXCLUDED.dataset_version;
                                                 INSERT INTO background_job (id, kind, payload, status, priority, created_at, started_at,
                                                     finished_at, attempts, stage, result_type, result_id)
                                                 VALUES (@id, 'ImportCsv', @payload, 'done', 0, now(), now(), now(), 1, @stage, 'csv_package', @hash)
                                                 ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status, finished_at = EXCLUDED.finished_at,
                                                     stage = EXCLUDED.stage, result_type = EXCLUDED.result_type, result_id = EXCLUDED.result_id;
                                                 """, connection);
        save.Parameters.AddWithValue("version", job.DatasetVersion);
        save.Parameters.AddWithValue("id", backgroundJobId ?? Guid.NewGuid());
        save.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, payload);
        save.Parameters.AddWithValue("hash", packageHash);
        save.Parameters.AddWithValue("stage", CompletedStage);
        save.CommandTimeout = 300;
        await save.ExecuteNonQueryAsync(cancellationToken);
        await WriteReportAsync(reportPath, report, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CsvImportResult(false, report);
    }

    private static async Task CopyCompaniesAsync(NpgsqlConnection connection, Stream stream, CancellationToken token)
    {
        using var reader = new CsvRecordReader(stream);
        await ValidateHeadersAsync(reader, CompanyHeaders, token);
        await using var copy = await connection.BeginBinaryImportAsync(
            "COPY csv_company (row_no, id, cl_type, okved, revenue, raw_data, rejection, quality_flags) FROM STDIN (FORMAT BINARY)",
            token);
        long rowNumber = 0;
        while (await reader.ReadAsync(token) is { } fields)
        {
            rowNumber++;
            var correctWidth = fields.Length == CompanyHeaders.Length;
            var raw = CompanyHeaders
                .Select((key, index) => new { key, value = index < fields.Length ? fields[index] : "" })
                .ToDictionary(item => item.key, item => item.value);
            var id = raw["SYNTHETIC_U"].Trim();
            var okved = raw["MAINOKVED_CODE"].Trim();
            var revenueText = raw["REVENUE_COMPANY"].Trim();
            var reason = correctWidth ? null : "invalid_column_count";
            decimal? revenue = null;
            var flags = new List<string>();
            if (revenueText.Length > 0)
            {
                if (!TryAmount(revenueText, RevenueLimit, out var amount))
                    reason ??= "invalid_revenue";
                else if (amount == 1000)
                    flags.Add("revenue_placeholder");
                else
                    revenue = amount;
            }

            if (!ValidOkved(okved))
                reason ??= "invalid_okved";
            foreach (var key in CompanyHeaders.Where(key => key.StartsWith("CL_PRODUCT_", StringComparison.Ordinal)
                                                            || key is "NEGATIVEMARK" or "FTS_FLAG" or "FACTORING_MARK"))
            {
                if (raw[key].Trim() is not ("" or "0.0" or "1.0"))
                    reason ??= "invalid_flag";
            }

            await copy.StartRowAsync(token);
            await copy.WriteAsync(rowNumber, NpgsqlDbType.Bigint, token);
            await copy.WriteAsync(id, NpgsqlDbType.Text, token);
            await copy.WriteAsync(raw["CL_TYPE"].Trim(), NpgsqlDbType.Text, token);
            await copy.WriteAsync(okved, NpgsqlDbType.Text, token);
            await WriteNullableAsync(copy, revenue, NpgsqlDbType.Numeric, token);
            await copy.WriteAsync(JsonSerializer.Serialize(raw), NpgsqlDbType.Jsonb, token);
            await WriteNullableAsync(copy, reason, NpgsqlDbType.Text, token);
            await copy.WriteAsync(JsonSerializer.Serialize(flags), NpgsqlDbType.Jsonb, token);
        }

        await copy.CompleteAsync(token);
    }

    private static async Task CopyPaymentsAsync(NpgsqlConnection connection, Stream stream, CancellationToken token)
    {
        using var reader = new CsvRecordReader(stream);
        await ValidateHeadersAsync(reader, PaymentHeaders, token);
        await using var copy = await connection.BeginBinaryImportAsync(
            "COPY csv_payment (row_no, payer, beneficiary, pay_amt, pay_cnt, rejection) FROM STDIN (FORMAT BINARY)",
            token);
        long rowNumber = 0;
        while (await reader.ReadAsync(token) is { } fields)
        {
            rowNumber++;
            string? reason = null;
            var payer = fields.Length > 1 ? fields[1].Trim() : "";
            var beneficiary = fields[0].Trim();
            decimal amount = 0;
            long count = 0;
            if (fields.Length != 4)
                reason = "invalid_column_count";
            else if (payer.Length == 0 || beneficiary.Length == 0)
                reason = "missing_participant";
            else if (payer == beneficiary)
                reason = "self_transfer";
            else if (!TryAmount(fields[2], AmountLimit, out amount))
                reason = "invalid_or_negative_amount";
            else if (!decimal.TryParse(fields[3], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                         CultureInfo.InvariantCulture, out var parsedCount)
                     || parsedCount < 0 || parsedCount > long.MaxValue || decimal.Truncate(parsedCount) != parsedCount)
                reason = "invalid_count";
            else
                count = (long)parsedCount;
            await copy.StartRowAsync(token);
            await copy.WriteAsync(rowNumber, NpgsqlDbType.Bigint, token);
            await copy.WriteAsync(payer, NpgsqlDbType.Text, token);
            await copy.WriteAsync(beneficiary, NpgsqlDbType.Text, token);
            await copy.WriteAsync(amount, NpgsqlDbType.Numeric, token);
            await copy.WriteAsync(count, NpgsqlDbType.Bigint, token);
            await WriteNullableAsync(copy, reason, NpgsqlDbType.Text, token);
        }

        await copy.CompleteAsync(token);
    }

    private static bool TryAmount(string text, decimal limit, out decimal amount) =>
        decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out amount)
        && amount >= 0 && amount < limit && decimal.Round(amount, 2) == amount;

    private static bool ValidOkved(string code)
    {
        var parts = code.Split('.');
        return parts.Length is >= 1 and <= 3 && parts[0].Length == 2
                                             && parts.All(part =>
                                                 part.Length is >= 1 and <= 2 && part.All(char.IsAsciiDigit));
    }

    private static async Task ValidateHeadersAsync(CsvRecordReader reader, string[] expected, CancellationToken token)
    {
        var actual = await reader.ReadAsync(token);
        if (actual is not null && actual.Length > 0)
            actual[0] = actual[0].TrimStart('\uFEFF');
        if (actual is null || !actual.SequenceEqual(expected))
            throw new InvalidDataException(
                "Заголовки CSV не соответствуют contracts/inputs.md (UTF-8, разделитель ;).");
    }

    private static async Task WriteNullableAsync<T>(NpgsqlBinaryImporter copy, T? value, NpgsqlDbType type,
        CancellationToken token)
    {
        if (value is null)
            await copy.WriteNullAsync(token);
        else
            await copy.WriteAsync(value, type, token);
    }

    private static async Task<CsvTableReport> ReadCompanyReportAsync(NpgsqlConnection connection,
        CancellationToken token)
    {
        var reasons = await ReadCountsAsync(connection,
            "SELECT coalesce(reason, 'accepted'), count(*) FROM csv_company_clean GROUP BY reason", token);
        var accepted = reasons.GetValueOrDefault("accepted");
        var read = reasons.Values.Sum();
        reasons.Remove("accepted");
        var quality = await ReadCountsAsync(connection, """
                                                        SELECT flag, count(*) FROM csv_company_clean,
                                                            jsonb_array_elements_text(quality_flags) AS flag WHERE reason IS NULL GROUP BY flag
                                                        """, token);
        return new CsvTableReport(read, accepted, accepted, 0, reasons, quality);
    }

    private static async Task<CsvTableReport> ReadPaymentReportAsync(NpgsqlConnection connection,
        CancellationToken token)
    {
        var reasons = await ReadCountsAsync(connection,
            "SELECT coalesce(rejection, 'accepted'), count(*) FROM csv_payment GROUP BY rejection", token);
        var read = reasons.Values.Sum();
        reasons.Remove("accepted");
        var totals = await ReadCountsAsync(connection, """
                                                       SELECT 'accepted', count(*) FROM csv_payment_groups WHERE rejection IS NULL
                                                       UNION ALL SELECT 'source', coalesce(sum(source_rows), 0)::bigint
                                                           FROM csv_payment_groups WHERE rejection IS NULL
                                                       """, token);
        var groupRejections = await ReadCountsAsync(connection, """
                                                                SELECT rejection, sum(source_rows)::bigint FROM csv_payment_groups
                                                                WHERE rejection IS NOT NULL GROUP BY rejection
                                                                """, token);
        foreach (var (reason, count) in groupRejections)
            reasons.Add(reason, count);
        reasons = reasons.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary();
        var quality = await ReadCountsAsync(connection, """
                                                        SELECT 'pairs_with_missing_profile', count(*) FROM csv_payment_groups p
                                                        WHERE rejection IS NULL AND (NOT EXISTS (SELECT 1 FROM csv_company_ids c WHERE c.id = p.payer)
                                                            OR NOT EXISTS (SELECT 1 FROM csv_company_ids c WHERE c.id = p.beneficiary))
                                                        """, token);
        return new CsvTableReport(read, totals["accepted"], totals["source"], totals["source"] - totals["accepted"],
            reasons, quality);
    }

    private static async Task<Dictionary<string, long>> ReadCountsAsync(NpgsqlConnection connection, string sql,
        CancellationToken token)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await using var reader = await command.ExecuteReaderAsync(token);
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token))
            counts.Add(reader.GetString(0), reader.GetInt64(1));
        return counts.ToDictionary();
    }

    private static FileStream OpenInput(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
        65536, FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static async Task<string> HashAsync(Stream stream, CancellationToken token)
    {
        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
        stream.Position = 0;
        return hash;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task WriteReportAsync(string path, CsvImportReport report, CancellationToken token)
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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
            { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        ContractJson.Configure(options);
        return options;
    }
}
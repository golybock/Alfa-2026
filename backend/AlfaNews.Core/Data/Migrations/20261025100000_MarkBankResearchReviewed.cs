using System.Text.Json;
using AlfaNews.Core.Documents;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AlfaNews.Core.Data.Migrations;

// Стартовые обзоры банка из сида распознавания проверены вручную: документы, распознанные страницы, блоки и графики
// становятся reviewed. Схема не меняется.
[DbContext(typeof(AlfaNewsDbContext))]
[Migration("20261025100000_MarkBankResearchReviewed")]
public sealed class MarkBankResearchReviewed : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(MarkBankResearchReviewed).Assembly.GetManifestResourceStream("BankResearchOcrSeed")
            ?? throw new InvalidOperationException("Не найден комплект распознанных документов.");
        using var json = JsonDocument.Parse(stream);
        var hashes = json.RootElement.GetProperty("documents").EnumerateArray()
            .Select(document => document.GetProperty("sha256").GetString()!).ToArray();
        foreach (var statement in ReviewedResearch.Statements(hashes))
            migrationBuilder.Sql(statement);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Pgvector;

namespace AlfaNews.Core.Ingest;

public sealed class NewsEmbeddings(HttpClient http, IConfiguration configuration)
{
    private readonly SemaphoreSlim gate = new(1);
    private float[][]? topics;
    private static readonly string[] TopicTexts =
    [
        "query: Экономика, бизнес, компании, банки, налоги, кредиты, инвестиции и регулирование.",
        "query: Промышленность, энергетика, сырье, торговля, экспорт, логистика и сельское хозяйство.",
        "query: Инфляция, ставка Центрального банка, курс валют, санкции и мировая экономика.",
        "query: Спорт, счет матча, голы, турнир, футбол, хоккей и результаты соревнований.",
        "query: Знаменитости, личная жизнь, развлечения, гороскопы, рецепты и советы по красоте."
    ];

    public async Task<NewsEmbeddingResult> EvaluateAsync(string title, string text, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            topics ??= await EmbedAsync(TopicTexts, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
        var input = title + "\n" + text;
        var vector = (await EmbedAsync(["passage: " + input[..Math.Min(input.Length, 12000)]], cancellationToken))[0];
        var scores = topics.Select(topic => Cosine(vector, topic)).ToArray();
        var business = scores.Take(3).Max();
        var unrelated = scores.Skip(3).Max();
        return new NewsEmbeddingResult(new Vector(vector), business, unrelated, unrelated >= 0.79 && unrelated - business >= 0.04);
    }

    public async Task<Vector> EmbedPassageAsync(string text, CancellationToken cancellationToken) =>
        new((await EmbedAsync(["passage: " + text[..Math.Min(text.Length, 12000)]], cancellationToken))[0]);

    public async Task<Vector> EmbedQueryAsync(string text, CancellationToken cancellationToken) =>
        new((await EmbedAsync(["query: " + text[..Math.Min(text.Length, 2000)]], cancellationToken))[0]);

    private async Task<float[][]> EmbedAsync(string[] inputs, CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Embeddings:BaseUrl"] ?? "http://127.0.0.1:18081";
        var uri = new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "embed");
        if (uri.Scheme != "http" || !(uri.Host == "tei" || uri.IsLoopback))
            throw new InvalidOperationException("Для e5 требуется локальный TEI.");
        using var response = await http.PostAsJsonAsync(uri, new { inputs, normalize = true, truncate = true }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var vectors = await response.Content.ReadFromJsonAsync<float[][]>(cancellationToken)
            ?? throw new InvalidOperationException("Пустой ответ e5.");
        if (vectors.Length != inputs.Length || vectors.Any(vector => vector.Length != 768
                || vector.Any(value => !float.IsFinite(value)) || vector.All(value => value == 0)))
            throw new InvalidOperationException("Неверная размерность ответа e5.");
        return vectors;
    }

    public static double Cosine(float[] left, float[] right)
    {
        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }
        return dot / Math.Sqrt(leftNorm * rightNorm);
    }
}

public sealed record NewsEmbeddingResult(Vector Vector, double BusinessScore, double UnrelatedScore, bool ClearlyUnrelated);
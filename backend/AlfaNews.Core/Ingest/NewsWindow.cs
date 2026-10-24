using AlfaNews.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace AlfaNews.Core.Ingest;

// Окно разбора новостей: публикации за последнюю неделю и не позже суток вперёд. При первом запуске источники отдают
// недельный запас; разбирать его целиком дорого, поэтому до первой собранной новости берём только InitialDays дней.
// Через Days − InitialDays дней работы окно снова становится обычной неделей. Окно ленты (A10) не меняется.
public static class NewsWindow
{
    public const int Days = 7;
    public const int InitialDays = 3;

    public static DateTimeOffset Start(DateTimeOffset now, DateTimeOffset? collectionStartedAt)
    {
        var week = now.AddDays(-Days);
        return collectionStartedAt is { } started && started.AddDays(-InitialDays) > week ? started.AddDays(-InitialDays) : week;
    }

    public static DateTimeOffset End(DateTimeOffset now) => now.AddDays(1);

    public static async Task<DateTimeOffset> StartAsync(AlfaNewsDbContext db, DateTimeOffset now, CancellationToken cancellationToken) =>
        Start(now, await db.NewsRawRows.AsNoTracking().MinAsync(row => (DateTimeOffset?)row.FetchedAt, cancellationToken));
}
namespace AlfaNews.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALFANEWS_TEST_CONNECTION")))
            Skip = "Задайте ALFANEWS_TEST_CONNECTION для интеграционных проверок PostgreSQL.";
    }
}
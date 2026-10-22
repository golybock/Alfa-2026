# THIRD_PARTY_NOTICES

Сторонние пакеты, добавленные в проект с согласия Артёма.

## backend (NuGet и локальные инструменты)

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | EF Core provider для PostgreSQL |
| Npgsql | 10.0.3, транзитивно | PostgreSQL | Драйвер PostgreSQL |
| Microsoft.EntityFrameworkCore / Relational / Abstractions / Analyzers | 10.0.4, транзитивно | MIT | ORM, конфигурации и миграции |
| Microsoft.EntityFrameworkCore.Design | 10.0.4 | MIT | Генерация миграций и snapshot |
| dotnet-ef | 10.0.4 | MIT | Локальный инструмент миграций |
| Pgvector.EntityFrameworkCore | 0.3.0 | MIT | Тип vector и запросы EF Core |
| Pgvector | 0.3.2, транзитивно | MIT | Значения vector(768) для Npgsql |
| Microsoft.OpenApi | 2.7.5 | MIT | Исправление CVE-2026-49451 в транзитивной зависимости ASP.NET OpenAPI |

## web (npm)

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| @tanstack/react-query | 5.104.5 | MIT | Запросы к API и кэш данных на фронте, единый Query provider |
| @tanstack/query-core | 5.104.5, зависимость @tanstack/react-query | MIT | Ядро @tanstack/react-query |
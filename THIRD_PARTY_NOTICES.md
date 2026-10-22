# THIRD_PARTY_NOTICES

Сторонние пакеты, добавленные в проект с согласия Артёма.

## backend (NuGet и локальные инструменты)

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| JsonSchema.Net | 7.3.4 | MIT | Проверка ответов LLM по JSON Schema Draft 2020-12 |
| JsonPointer.Net | 5.3.1, транзитивно | MIT | JSON Pointer для валидатора схем |
| Json.More.Net | 2.1.1, транзитивно | MIT | JSON-операции для валидатора схем |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | EF Core provider для PostgreSQL |
| Npgsql | 10.0.3, транзитивно | PostgreSQL | Драйвер PostgreSQL |
| Microsoft.EntityFrameworkCore / Relational / Abstractions / Analyzers | 10.0.4, транзитивно | MIT | ORM, конфигурации и миграции |
| Microsoft.EntityFrameworkCore.Design | 10.0.4 | MIT | Генерация миграций и snapshot |
| dotnet-ef | 10.0.4 | MIT | Локальный инструмент миграций |
| Pgvector.EntityFrameworkCore | 0.3.0 | MIT | Тип vector и запросы EF Core |
| Pgvector | 0.3.2, транзитивно | MIT | Значения vector(768) для Npgsql |
| BCrypt.Net-Next | 4.2.0 | MIT | Хэширование паролей пользователей (D04) |
| AngleSharp | 1.8.5 | MIT | Разбор HTML статей новостных источников (D08) |
| SmartReader | 0.11.1 | Apache-2.0 | Выделение основного текста статьи без меню и рекламы (D08) |
| Newtonsoft.Json | 13.0.3, транзитивно (SmartReader) | MIT | Зависимость SmartReader |
| Microsoft.OpenApi | 2.7.5 | MIT | Исправление CVE-2026-49451 в транзитивной зависимости ASP.NET OpenAPI |

## web (npm)

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| @tanstack/react-query | 5.104.5 | MIT | Запросы к API и кэш данных на фронте, единый Query provider |
| @tanstack/query-core | 5.104.5, зависимость @tanstack/react-query | MIT | Ядро @tanstack/react-query |
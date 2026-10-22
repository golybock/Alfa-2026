# A02: БД и доступ

Первая миграция создаёт 28 таблиц из `contracts/database.sql`, включая user_activity. SQL-контракт описывает целевую схему, а применяются только миграции EF Core. JSONB не раскладывается в дополнительные таблицы. У payment нет FK на company. FTS заполняют обработчики через `to_tsvector('russian', ...)`, embedding — nullable vector(768).

## Запуск и миграции

`docker compose up -d --build` запускает отдельный сервис migrate перед API и Worker. Он выполняет `dotnet AlfaNews.Api.dll --migrate`, применяет новые миграции и завершается. API/Worker сами схему не меняют. `docker compose logs migrate` показывает результат. Для повторной явной проверки: `docker compose run --rm migrate`.

Для EF CLI на хосте задайте `ConnectionStrings__DefaultConnection` через окружение, затем:

```powershell
dotnet tool restore
dotnet ef migrations list --project backend/AlfaNews.Core
dotnet ef database update --project backend/AlfaNews.Core
```

PostgreSQL в штатном Compose доступен с хоста по `127.0.0.1:15432`; внутри Compose — `postgres:5432`. Для CLI задайте `Host=127.0.0.1;Port=15432`, а имя БД, пользователя и пароль возьмите из `.env`. Не передавайте строку подключения через аргументы CLI или исходный код.

## Подключение в D04/D09/D10

Оба хоста используют `AddAlfaNewsData(connectionString)`. Сервисы получают AlfaNewsDbContext напрямую через DI; сущности и конфигурации находятся в Core/Data. DbContext scoped; Worker создаёт DI-scope на операцию.

В API зарегистрированы SessionService, AccessService, UserActivityService, схема аутентификации Session и политика Operator. D04 создаёт криптографически случайный токен, сохраняет только `SessionService.HashToken(token)` в auth_session, а оригинал — в cookie `alfa_session` с HttpOnly, Secure в production и SameSite=Lax. Проверка пароля, login/logout/me и демо-учётки относятся к D04. Пароли не хранятся в первой миграции; синтетические учётки с отключённым входом создаются только в тестовых базах.

Защищённая группа endpoints использует `RequireAuthorization()`. Операторская — `RequireAuthorization(SessionAuthenticationHandler.OperatorPolicy)`. UserContext извлекается через `SessionAuthenticationHandler.CurrentUser(http.User)`. Изменяющие запросы `/api` проверяет ApiAccessMiddleware: Origin обязателен; для внешнего HTTPS задайте `Auth__AllowedOrigins__0` и последующие индексы в env. Доверие к произвольным X-Forwarded-* не включено. Ошибки AccessException преобразуются в ApiError.

Перед чтением/постановкой работы вызывайте соответствующую проверку с CancellationToken:

- RequireCompanyAccessAsync: точная company × CEO/CFO; IsOperator не даёт обхода.
- RequireInsightAsync: сохранённые company/role карточки; идентификаторы из тела запроса их не заменяют.
- RequireEvidenceAsync: evidence ID внутри доступной ready-карточки.
- RequireResearchPageAsync: только конкретная document/page, упомянутая в доступной ready-карточке, либо оператор. Возвращённый image_path остаётся внутри API.
- RequireJobAsync: владелец или оператор; company_id задания сам по себе не открывает его другим пользователям.
- ReadChatHistoryAsync: только текущий пользователь × доступная карточка.

Company/role проверяются при создании доверенного контекста работы. LLM не получает возможность выбрать company_id. Внешнему API возвращайте DTO, а не ORM-сущности.

## Журнал

Реализован `POST /api/insights/{id}/activity` по UserActivityRequest. Время, пользователь и компания — серверные. Ссылки проверяются по карточке. UUID события сохраняется браузером при повторе доставки; точный повтор идемпотентен. Для принятого чата D09/A13 вызывают RecordAcceptedChatAsync после SaveChangesAsync внутри той же транзакции сохранения вопроса. ID события равен chat_message.id; повтор выполнения задания не удваивает метрику. Браузер не может отправить chat_question_accepted. Тексты и произвольные свойства в журнал не попадают. Подключение UI-событий выполняется вместе с соответствующими экранами.

## Проверки

```powershell
dotnet build Alfa-2026.slnx
dotnet format Alfa-2026.slnx --verify-no-changes --no-restore
dotnet test backend/AlfaNews.Tests
```

Интеграционные тесты требуют `ALFANEWS_TEST_CONNECTION` в окружении: строка подключения к отдельному PostgreSQL 17 с установленным pgvector и правом создания БД. Без неё PostgreSQL-тесты явно пропускаются. Каждый fixture создаёт случайную `alfanews_test_*` БД, дважды применяет миграцию, добавляет только синтетические данные и удаляет эту БД после проверки. Основная БД приложения не очищается.

Тесты сравнивают SQL по колонкам, ключам, CHECK/FK и индексам; проверяют JSONB/FTS/vector, доступы/сессии/страницы, приватность истории, идемпотентность журнала и реальный HTTP endpoint с cookie/Origin. Для HTTP-теста требуется `dotnet`; API запускается дочерним процессом на loopback и останавливается в finally.

Чистая Docker-сборка: `docker compose -p alfa-news -f compose.yaml build --no-cache`. Для пересоздания только контейнеров проекта сначала проверьте `docker compose ls` и `docker compose -p alfa-news -f compose.yaml ps -a`, затем выполните `docker compose -p alfa-news -f compose.yaml down` без `--volumes` и поднимите проект снова. Не используйте docker system/container/volume prune.
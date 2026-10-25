# AGENTS.md

Инструкции для AI-ассистентов (Claude, Cursor, Copilot и т.п.), которые помогают команде писать код в этом репозитории. Прочитай файл целиком перед первой правкой.

## Проект

Сервис «Новостная аналитика» для хакатона Альфа-Банка. ЛПР крупной компании видит в интернет-банке Альфа-Бизнеса не ленту новостей, а события, отобранные под его компанию и роль, с объяснением влияния, рисками, возможностями, позицией банка и ссылками на первоисточники.

Конвейер:

1. Worker собирает новости (RSS, официальные источники) и аналитику банка (PDF), разбирает каждый документ один раз и сохраняет факты и теги в Postgres.
2. Профиль компании считается из CSV компаний и платежей кодом и SQL; группы связываются по GROUP_ID. Связи через физлиц отложены и не импортируются.
3. Ранжирование — формула в коде; каждое слагаемое становится причиной «почему показано».
4. Агент (C#-оркестратор + LLM) строит персональную интерпретацию события только на доказательствах из базы. Он же отвечает на уточняющие вопросы в чате.
5. Next.js показывает мок главной страницы Альфа-Бизнеса, ленту, карточку события, чат и источники.

Дедлайн жёсткий. Работающее и проверенное важнее красивого и большого.

## Карта текущей реализации

Сверено 23.10.2026 по файлам рабочего дерева, включая незакоммиченные изменения, плану, FigJam и задачам Linear. Это карта для навигации, а не подтверждение сквозной готовности. При расхождении фактическое поведение проверяй по коду, DI-регистрации в `Program.cs` и тестам; наличие DTO, таблицы, промпта или метода mock-клиента не означает наличие обработчика на backend.

### Где искать задачу

Все пути ниже относительно корня репозитория.

| Что нужно найти | Начальная точка |
| --- | --- |
| Запуск, адреса, Docker и проксирование | `README.md`, `compose.yaml`, Dockerfile проектов, `web/next.config.ts` |
| Формат HTTP, DTO, JSON Schema и примеры | `contracts/README.md`, `contracts/http.md`, `backend/AlfaNews.Contracts/`, `contracts/schemas/`, `contracts/examples/` |
| Таблицы, JSONB, индексы и миграции | `docs/data-model.md`, `contracts/database.md`, `backend/AlfaNews.Core/Data/` |
| Логин, сессия, компания и роль | `backend/AlfaNews.Api/Access/`, `backend/AlfaNews.Core/Access/`, `web/src/lib/hooks/session.ts` |
| Очистка и массовый импорт CSV | `backend/AlfaNews.Core/Importing/CsvImporter.cs`, `backend/AlfaNews.Worker/Importing/`, `docs/a03-csv-import.md` |
| Признаки, группа, продукты, страны и платёжные доли | `backend/AlfaNews.Core/Profiles/`, `backend/AlfaNews.Worker/Profiles/`, `docs/a04-profiles.md` |
| Очередь, расписание, приоритеты и повтор задания | `backend/AlfaNews.Core/Processing/`, `backend/AlfaNews.Worker/Queue/`, `docs/a05-job-queue.md` |
| Источники, RSS, HTML и защита сетевых запросов | `data/sources.json`, `data/sources-specialized.json`, `backend/AlfaNews.Core/Ingest/`, `docs/d08-ingest.md` |
| Важность заголовка, e5-фильтр, дубли, извлечение события | `NewsProcessingSchedule.cs`, `NewsTriageService.cs`, `NewsProcessing.cs`, `NewsEmbeddings.cs` в `backend/AlfaNews.Core/Ingest/`; `docs/a07-news-processing.md` |
| PDF, OCR, PNG страниц и перенос распознанных документов | `backend/AlfaNews.Core/Documents/`, `backend/AlfaNews.Worker/Documents/`, `docs/a08-pdf-ocr.md` |
| Модели, бюджет, кэш, проверка ответа и промпты | `backend/AlfaNews.Core/Llm/`, `backend/AlfaNews.Core/Prompts/`, `docs/a06-llm-gateway.md` |
| Чтение ленты и карточки, постановка их генерации | `backend/AlfaNews.Api/Feed/`, `backend/AlfaNews.Api/Insights/`, одноимённые каталоги в Core |
| Главная банка, виджет, лента, карточка, чат и evidence | `web/src/app/(bank)/`, `web/src/components/{bank,feed,card}/`, `web/src/lib/hooks/queries.ts` |
| HTTP-клиент, mocks, типы и фикстуры сайта | `web/src/lib/api/`, `web/src/lib/types/api.ts`, `web/fixtures/` |
| Журнал действий пользователя | `backend/AlfaNews.Api/Activity/`, `backend/AlfaNews.Core/Activity/UserActivityService.cs` |
| Проверки конкретной подсистемы | `backend/AlfaNews.Tests/`; карта тестов и команды ниже |

### Структура репозитория

```text
Alfa-2026.slnx                       решение .NET в корне, не backend/AlfaNews.sln
backend/
  AlfaNews.Api/                     ASP.NET Core minimal API: доступ, чтение, постановка jobs
  AlfaNews.Worker/                  процесс фоновой обработки и ручные команды
    Importing/                     запуск импорта CSV и его job handler
    Profiles/                      запуск сборки профилей и его job handler
    News/                          handlers CollectNews, TriageNews, ParseNews
    Documents/                     сканер PDF, расписание и OCR handlers
    Queue/                         цикл Worker, расписание и тестовое задание
    Evaluation/                    LlmEvaluation и NewsValidation
  AlfaNews.Core/                    прикладная логика, EF Core и инфраструктура
    Access/ Activity/ Feed/ Insights/
    Importing/ Profiles/ Processing/ Ingest/ Documents/ Llm/
    Configuration/                 загрузка локального окружения
    Data/                          DbContext, Entities, Configurations, Migrations и Seeds
    Prompts/                       шаблоны LLM, манифест версий, fixtures и проверки
  AlfaNews.Contracts/               отдельный проект общих records/enum и сериализации
    Domain/                        профили, claims/evidence, ранжирование, статусы
    Api/                           входные и выходные HTTP DTO
    Processing/                    задания Worker и структурированные ответы LLM
  AlfaNews.Tests/                   xUnit: unit-, HTTP- и PostgreSQL-тесты
web/
  src/app/                         Next.js App Router: login, select, группа (bank)
  src/components/                  auth, layout, bank, feed, card, ui
  src/lib/api/                     общий интерфейс, HTTP и mock-реализации
  src/lib/hooks/                   сессия, TanStack Query, просмотр evidence
  src/lib/types/                   публичные API-типы, типы оболочки банка и иконок
  fixtures/api/                    копии контрактных примеров для mock API
  fixtures/                        users.json и bank-home.json для демонстрационной оболочки
  public/                          изображения и mock-page.svg
contracts/                         спецификация HTTP/БД, api.ts, schemas, examples, generate.py
data/                              входные CSV, девять PDF, каталоги источников и отчёты
  bank_research/index.json          метаданные имеющихся PDF по SHA-256
  taxonomy.json                    факторы, правила продуктов и чувствительность отраслей (черновик, загружается Worker при старте)
  reports/                         import_report.json, profile_candidates.json и сравнение покрытия
  seed/                            снимок базы demo.sql.gz для запуска без ключа; PostgreSQL загружает его в пустую базу
tools/
  NewsCollector/                   пока консольный шаблон; рабочий сбор находится в Core/Ingest
  export-research-seed.py           экспорт подтверждённого OCR из PostgreSQL во встроенный seed
  export-demo-seed.py               экспорт снимка базы в data/seed/demo.sql.gz (без сессий, кэша модели и полных текстов новостей)
docs/                              продуктовые 00–05 и diagrams/, описания реализации A02–A13, D04, D08, модель данных, eval-report, test-scenarios
compose.yaml                       PostgreSQL, TEI, migrate, API, Worker, web, nginx gateway
.env.example                       имена настроек без рабочих секретов
README.md                          локальный запуск и эксплуатационные оговорки
CONTRIBUTING.md                     правила работы с репозиторием и исключения из Git
THIRD_PARTY_NOTICES.md              сторонние зависимости
```

Папок `backend/src`, `backend/tests`, `web/app` и `web/lib` в этой структуре нет. Утилиты `LlmProbe` нет; проверки LLM находятся в Worker/Evaluation. `Core/Class1.cs` и `Tests/UnitTest1.cs` — остатки шаблонов, не точки входа предметной логики.

### Backend: ответственность модулей

| Модуль | Как устроен и что выполняет |
| --- | --- |
| `Api/Program.cs` | Загружает окружение, регистрирует сервисы и аутентификацию, применяет миграции, подключает endpoints. Команда `--migrate` завершает процесс после миграций. Демо-учётки создаёт Worker после импорта CSV (`UserSeedService`). В Development доступны `/api/swagger` и `/api/openapi/v1.json`. |
| `Core/Configuration/LocalEnvironment.cs` | Читает ближайший родительский `.env`; переменные процесса имеют приоритет. Формирует локальную строку PostgreSQL из `POSTGRES_*`, если она не задана явно. Секреты не выводить. |
| `Api/Access/` + `Core/Access/` | `AuthEndpoints` — login/logout и `/me`; `AuthService` — пароль и выдача сессии; `SessionService` и `SessionAuthenticationHandler` — проверка токена. `ApiAccessMiddleware` обрабатывает доступ/Origin и ошибки. `AccessService` проверяет компанию, роль, карточку, evidence, страницу PDF, владельца job и личную историю чата. Выбранные в UI company/role не являются авторизацией. |
| `Core/Data/` | `AlfaNewsDbContext.cs` — таблицы; `Entities/` — ORM-сущности; `Configurations/` — отображение и ограничения; `DataRegistration.cs` — PostgreSQL/pgvector; `AlfaNewsDbContextFactory.cs` — EF tooling. Изменение целевого SQL в contracts само по себе не меняет базу: выполняются EF-миграции. |
| `Core/Importing/` + `Worker/Importing/` | `CsvRecordReader` читает CSV; `CsvImporter` очищает данные и загружает их через Npgsql COPY и SQL в транзакции. `CsvImportService` выбирает файлы/версию и управляет стартовым запуском; `ImportCsvJobHandler` подключает это к очереди. |
| `Core/Profiles/` + `Worker/Profiles/` | `ProfileBuilder` считает SQL-агрегаты платежей и профили; `CompanyProfileMapper` преобразует очищенные поля и продукты; `CountryNames` — общий словарь стран; `ProfileReportBuilder` формирует отчёт кандидатов. `ProfileBuildService` и `RebuildProfilesJobHandler` управляют запуском. |
| `Core/Processing/` + `Worker/Queue/` | `JobKinds` сопоставляет payload, kind и приоритет; `JobQueue` ставит, ищет, восстанавливает и повторяет jobs; `JobDispatcher` выбирает по приоритету/времени и вызывает зарегистрированный `IJobHandler`. `Worker` исполняет задания полосами data/news/documents/insights/chat: внутри полосы по приоритету, полосы параллельно (`JobKinds.InLane`), число исполнителей полосы — `QueueOptions` из `.env` (`QUEUE_*_SLOTS`), обработчик может отложить задание (`JobResult.Defer`); `JobScheduler`/`JobSchedule` ставят работу по расписанию. `TestJobScheduler`/`ConsoleMessageJobHandler` — выключенная по умолчанию проверка очереди. |
| `Core/Ingest/` + `Worker/News/` | `SourceCatalogLoader` синхронизирует каталоги с `source`; `SafeFetcher` ограничивает адреса, редиректы, время и размер; `RssReader` разбирает ленты; `ArticleExtractor` извлекает тело; `TextNormalizer` нормализует текст/хэш; `CollectNewsService` сохраняет `news_raw`. Дальше работают классификация заголовков и разбор события, описанные ниже. |
| `Core/Documents/` + `Worker/Documents/` | `PdfOptions` задаёт пути/лимиты; `PdfTools` вызывает `pdfinfo`/`pdftoppm`; `PdfRegistration` регистрирует PDF и job по хэшу; `PdfScan`/`PdfScheduler` обходят входные каталоги; `PdfProcessing` готовит страницы, вызывает OCR и финализирует документ; `PdfExtractionChecks` проверяет извлечение. `PdfJobHandlers.cs` связывает три операции с очередью. |
| `Core/Llm/` + `Core/Prompts/` | `LlmGateway` — единая точка генеративных вызовов, кэша, ограниченных повторов и починки. `LlmRegistration` выбирает `MockLlmClient` или `OpenAiApiClient`; последний — HTTP-транспорт к шлюзу организаторов, не разрешение использовать другие модели. `LlmBudget` учитывает резерв/расход; `LlmPromptCatalog` рендерит версии шаблонов; `LlmOutputValidator` и `LlmSemanticChecks` проверяют JSON и смысловые ограничения. |
| `Core/Feed/FeedService.cs` | Читает сохранённый `feed_snapshot`, убирает скрытые пользователем события, добавляет статус карточки выбранной роли. При отсутствии снимка возвращает пустую ленту. `RefreshAsync` ставит `RefreshFeedJob`. Формула и запись снимка — `FeedRanking`/`FeedRefreshService` в той же папке, словари — `TaxonomyCatalogLoader` из `data/taxonomy.json` ([A10](docs/a10-ranking.md)). |
| `Core/Insights/InsightService.cs` | Проверяет доступ к company/event/role, читает готовый `insight.body_json` или возвращает ожидание. `EnsureAsync` ставит `GenerateInsightJob`. Готовая карточка возвращается без нового LLM-вызова; failed требует явного повтора. Генерацию выполняет `InsightAgent` (план, read-only `AgentTools`, черновик, `InsightChecks`, критик, одна починка; [A11](docs/a11-agent.md)). |
| `Core/Activity/UserActivityService.cs` | Записывает открытие карточки, причин, источника, действие и принятый вопрос без текста чата. Проверяет ссылки на карточку и защищает повтор одного event ID. `FeedbackService` сохраняет like/dislike/hide на событие компании для пользователя. Endpoints действий, отзыва и evidence находятся в `Api/Activity/`. |
| `Core/Chat/` + `Worker/Chat/` | `ChatService` принимает вопрос по готовой карточке (лимиты, повтор, история) и ставит `AnswerChatJob`; `ChatResponder` выполняет scope gate и один ответ `ChatDraft` по контексту карточки. Endpoints — `Api/Chat/` ([A13](docs/a13-chat.md)). |

### Как проходят данные

1. **CSV → профили.** При старте Worker восстанавливает очередь, затем выполняет разрешённые конфигом импорт и сборку профилей. Читаются только `df_Uid_data_hack.csv` и `df_Uid_payment_hack.csv`. Файл `df_Fid_Uid_link_hack.csv` не используется. Очистка исключает конфликтующие ID и самопереводы, переводит выручку-заглушку 1000 в неизвестное значение, фильтрует известную суммарную выручку CLIENT-группы ниже 15 млрд ₽. Группа без известной выручки и компания без группы сохраняются; платежи обрабатываются по правилам A03. Результат — `company`, `payment`, затем `company_profile` и `group_profile`, отчёты в `data/reports/`. Актуальность определяется версией/хэшами данных и алгоритма. Платёжные доли и покрытие считаются отдельно по направлениям; неизвестная отрасль не исключается из знаменателя. Платежи не доказывают отношения поставки.
2. **RSS/HTML → исходная новость.** Каталоги `sources.json` и `sources-specialized.json` загружаются при старте. Планировщик каждые 30 секунд проверяет сроки источников и ставит отдельные `CollectNewsJob`; фактическое исполнение последовательное. Текущие интервалы общего и специализированного потоков — 3600 и 900 секунд соответственно. Сборщик проверяет allowlist, URL, дату и недельное окно, сохраняет текст/метаданные/статус в `news_raw`. Конкретные хосты и настройки ищи в каталогах и `SourceCatalogLoader`, а не в старом количестве источников из README.
3. **Исходная новость → событие.** `NewsProcessingSchedule` ставит `TriageNewsJob` пакетами до 50 заголовков и ограничивает активные `ParseNewsJob` числом 50. `NewsTriageService` получает через LlmGateway yes/maybe/no и сохраняет importance 1/2/3; специализированные источники сразу получают 1. Сначала разбирается 1, затем 2; 3 отклоняется. `NewsProcessing` проверяет источник/дату/тело, связывает точные дубли через `event_source`, вызывает локальный e5 и `NewsExtraction` через LlmGateway. Похожесть e5 — кандидат в дубли и осторожный фильтр посторонних тем, а не автоматическое объединение. Принятый результат транзакционно сохраняется в `event`, `event_source`, `event_tag` с FTS и вектором. После сохранения событие проходит скоринг для `Feed:CompanyIds`; при прохождении порога ставится RefreshFeed компании. Цитаты фактов вырезает код по номеру строки и диапазону от модели (`NewsLines`); при недоступном TEI e5 пропускается (`e5_unavailable`).
4. **PDF → страницы и банковские блоки.** `PdfScheduler` обходит PDF в `Data:InputPath` и `Storage:RootPath/inbox` при старте и раз в 15 минут. Оператор также может загрузить PDF через `POST /api/admin/documents` (до 50 MiB и 10 страниц). Метаданные известных документов берутся из `data/bank_research/index.json`. Новый хэш создаёт `IndexPdfJob` → `OcrPdfPageJob` для страниц → `FinalizePdfJob`. Рендер страницы передаётся GLM через LlmGateway; сохраняются `research_doc`, `research_page`, `research_chunk`, таблицы, графики и FTS. OCR-ready не означает ручную сверку: результаты остаются `not_reviewed`. Финализация ставит `EmbedContentJob`; `ResearchSearch` считает e5-вектор фрагмента и ищет FTS+e5 через RRF. Планировщик сам индексирует фрагменты без вектора, включая seed.
5. **База → сайт.** Nginx направляет `/api/` в API, остальное в Next.js. Реальные endpoints читают ленту/карточку и ставят jobs с `202`; клиент опрашивает `/api/jobs/{id}`. В `web/src/lib/hooks/queries.ts` интервал ленты 120 секунд, задания — 3 секунды до done/failed. SSE в текущем MVP не реализован. UI по умолчанию демонстрирует контрактные mocks, поэтому успешный сценарий на сайте не доказывает готовность backend генерации.
6. **Будущий полный инсайт и чат.** Целевой путь — формула ранжирования → поиск банковских доказательств → собственный агент с read-only инструментами → проверки/критик/одна починка → сохранение карточки; чат отвечает по готовой карточке. Сейчас для этого есть контракты, таблицы, шаблоны и mock UI, но не весь исполняемый путь. Смотри границы готовности ниже перед планированием изменений.

### Фронтенд: страницы и состояние

| Путь | Назначение |
| --- | --- |
| `web/src/app/layout.tsx`, `providers.tsx`, `globals.css` | Корневой layout, QueryClientProvider, общие стили. |
| `web/src/app/login/page.tsx`, `select/page.tsx` | Вход и выбор доступной компании/роли; формы в `components/auth/`. |
| `web/src/app/(bank)/layout.tsx`, `components/layout/` | Авторизованная оболочка, шапка, меню и проверка выбранного контекста. |
| `web/src/app/(bank)/page.tsx`, `components/bank/`, `components/feed/InsightsWidget*.tsx` | Мок главной Альфа-Бизнеса и виджет новостной аналитики. |
| `web/src/app/(bank)/analytics/page.tsx`, `components/feed/FeedScreen.tsx` | Лента событий, обновление, пустые состояния и переход в карточку. |
| `web/src/app/(bank)/analytics/[eventId]/page.tsx`, `components/card/InsightCardScreen.tsx` | Экран карточки. `InsightCardBody`/`CardBlock`/`ClaimList` собирают содержимое; `WhyPanel` показывает причины; `ChatPanel`/`ChatTurn` — уточняющие вопросы. |
| `components/card/EvidenceViewerProvider.tsx`, `EvidenceDetail.tsx`, `lib/hooks/evidenceViewer.ts` | Открытие evidence и страницы источника. Общие кнопки/бейджи/панели находятся в `components/ui/`. |
| `web/src/app/(bank)/sections/[section]/page.tsx` | Заглушки остальных разделов банка, не отдельные реализованные банковские сервисы. |
| `web/src/lib/api/index.ts`, `mode.ts`, `client.ts`, `http.ts`, `mock.ts` | `Api` определяет интерфейс; `index.ts` выбирает HTTP или mocks по `NEXT_PUBLIC_USE_MOCKS`; `client.ts` содержит общий fetch и ошибки; `http.ts` — пути API; `mock.ts` — синтетическое поведение. `bank.ts` обслуживает мок банковской оболочки. |
| `web/src/lib/hooks/session.ts` | `/me`, состояния входа, выбранные company/role в localStorage, очистка кэша при смене компании. Сервер независимо перепроверяет права через `AccessService`. |
| `web/src/lib/hooks/queries.ts`, `web/src/lib/claimKinds.ts` | TanStack Query, мутации, polling и ключи кэша company/event/role; отдельный файл цветов/подписей видов утверждений. `dates.ts`, `labels.ts`, `navigation.ts` — даты, подписи и маршруты UI. |

`web/fixtures/api/` зеркалирует `contracts/examples/`, `web/src/lib/types/api.ts` — `contracts/api.ts`. Команда `npm run contracts:sync` копирует их и меняет файлы: не запускай её для простого чтения проекта. `NEXT_PUBLIC_USE_MOCKS` встраивается при сборке Next.js; сейчас `web/Dockerfile` фиксирует true на этапе build, поэтому изменение только runtime environment Compose не переключит собранный клиент. Для отдельного локального web-сервера `API_PROXY_URL` включает rewrite `/api/` в `next.config.ts`.

### Контракты, хранение и промпты

- Начинай с `contracts/README.md`: `http.md` описывает целевые методы и права, `inputs.md` — реальные входные файлы, `database.md`/`database.sql` — целевую структуру хранения. Сверяй каждый endpoint с `Api/*Endpoints.cs`: спецификация содержит ещё не реализованные маршруты.
- C# records/enum лежат в `backend/AlfaNews.Contracts/{Domain,Api,Processing}/`, общие настройки JSON — в `ContractJson.cs`. `contracts/generate.py` читает эти типы и записывает `contracts/api.ts` и `contracts/schemas/`; это генератор, не команда проверки без изменений. Менять контракт и запускать генерацию можно только в согласованной задаче.
- JSON использует snake_case и строковые enum; `null` означает неизвестное, 0 — известный ноль. Публичные типы не раскрывают исходные платежи, пароли, пути хранения или промпты. Claims и evidence хранятся внутри JSONB карточки, а не в отдельных таблицах.
- Для текущих имён происхождения утверждений смотри enum `Origin` в Contracts/Domain и `web/src/lib/claimKinds.ts`: `source_fact`, `bank_analytics`, `ai_inference`, `ai_hypothesis`. Не путай их с видами OCR-блоков `fact`, `bank_view`, `forecast`, `disclaimer` и общими обозначениями в старом описании проекта.
- Группы таблиц: доступ — `app_user/auth_session/user_company_access`; компания — `company/payment/company_profile/group_profile`; словари — `factor/sector_sensitivity/product_factor_rule`; новости — `source/news_raw/event/event_source/event_tag`; PDF — `research_doc/research_page/research_chunk`; выдача — `feed_snapshot/insight/chat_message/feedback/user_activity`; фон и LLM — `background_job/agent_run/agent_step/llm_usage/llm_cache`. Подробности — `docs/data-model.md` и EF-конфигурации.
- В `Data/Migrations/` находятся `InitialSchema`, `LlmAttemptReservations`, `SeedBankResearchOcr`, `NewsImportanceAndSourceCatalog` и snapshot. `Seeds/bank-research-ocr.json` переносит сохранённый результат OCR девяти PDF без повторных LLM-вызовов; `Seeds/news-sources.json` — снимок каталога для миграции. Seed OCR не ставит ручную отметку сверки и не заменяет доступность исходных PDF.
- `Core/Prompts/` содержит пары `*.system.txt`/`*.user.txt` для news-triage, news-extract, pdf-ocr, agent-plan, agent-draft, critic, repair, chat, scope-gate; `versions.json` фиксирует версии, placeholders и хэши. `fixtures/` используется проверками; входной JSON для ручного eval передаётся отдельным аргументом Worker. Схемы при build/publish копируются из `contracts/schemas/`, отдельную версию схем в промптах не редактировать.
- `LlmOptions.cs` и appsettings/окружение определяют реальную маршрутизацию: DeepSeek для новостей и текстовых задач, GLM для OCR/критика; triage может переключаться на GLM настройкой. Текущее значение GLM reasoning — low, несмотря на high в раннем плане и схемах. Шаблон агента/критика сам по себе не реализует orchestration, инструменты и проверку полной карточки.

### Границы готовности и сопоставление с задачами

Состояния Linear — снимок на 23.10.2026, не замена проверке кода и живой приёмке. Ищи задачи по идентификатору в [проекте Linear](https://linear.app/alfa-hackathon).

| Область | Фактическая граница и задачи |
| --- | --- |
| Основа, контракты, доступ, CSV, профили, очередь, LLM | Реализации находятся по карте выше; A01/ALF-41, A02/ALF-42, A03/ALF-43, A04/ALF-5, A05/ALF-44, A06/ALF-45 и промпты ALF-53 отмечены Done. |
| Сбор и обработка новостей | D08/ALF-31 — Done; A07/ALF-6 — In Progress, в рабочем дереве есть triage/e5/ParseNews. ALF-57 (ссылки на фрагменты вместо генерируемых цитат) и ALF-58 (качество фильтра заголовков) — Backlog; не считать их уже выполненными. Для ALF-58 подготовлены инструкция разметки и скрипт замера ([docs/alf58-triage-labeling.md](docs/alf58-triage-labeling.md)), разметки и нового промпта нет. |
| PDF | A08/ALF-46 — In Progress, хотя OCR, встроенный seed, upload/list/detail, выдача PNG и retry уже есть. Embeddings и поиск банковской аналитики (A09/ALF-7, шаги 1–3) реализованы; ручная сверка страницы (`ResearchReview`, маршрут review в `DocumentEndpoints.cs`) — в рабочем дереве; зависимые карточки не помечаются. D10/ALF-33 и UI оператора D11/ALF-34 — Todo. |
| Лента/карточка API | D09/ALF-32 — In Progress: есть чтение сохранённых результатов, постановка jobs и GET job. Ранжирование и снимок A10/ALF-47 реализованы (`FeedRefreshService`), обработчик RefreshFeed подключён. |
| Агент и чат backend | A11/ALF-8 и A12/ALF-48 реализованы в `Core/Insights/InsightAgent.cs`; чат A13/ALF-49 — в `Core/Chat/` (в рабочем дереве, живой прогон на модели не выполнялся). Демо-снимок `PrecomputeDemo` и режим `Feed:Mode` — в рабочем дереве ([A10](docs/a10-ranking.md)). Ручная новость по ссылке D17 (`POST /admin/news`, `CollectManualNewsJobHandler`) сделана 25.10. |
| Сайт и интеграция | D01–D07 (ALF-24–ALF-30) — Done, включая UI чата на mocks. Endpoints чата, evidence JSON и feedback есть в backend, но сайт пока работает на mocks. D12/ALF-35 — Todo; переход всего UI на живой backend D14/ALF-37 — Todo. |
| Финальная приёмка | Замеры A14/ALF-50, интеграция A15/ALF-51, сквозной сигнал ALF-54, персонализация ALF-55, ручные проверки K03/K05/K06 — отдельная незавершённая работа. Статус Done отдельных частей не означает готовность этих сценариев. |

По плану MVP включает две компании разных отраслей × CEO/CFO, до 3–5 подходящих сигналов за последние семь дней, общий score компании и разные акценты карточки для ролей. Поиск банковской позиции не должен менять score. MMR, реранкер, кластеризация, связи физлиц, продуктовые предложения, архив и push отложены; количественный сценарий top-1 и расширения оператора не следует принимать за обязательные уже существующие модули.

### Запуск, данные и проверки для навигации

- Штатный путь: `README.md` → локальный `.env` по `.env.example` → `docker compose up -d --build`. `compose.yaml` поднимает PostgreSQL 17 с pgvector, локальный TEI e5, одноразовый `migrate`, API, один Worker, web и nginx gateway. API и Worker также применяют миграции при старте. Приложение — `http://localhost:8080` по умолчанию, PostgreSQL — `127.0.0.1:15432`, TEI — `127.0.0.1:18081`; внутренние адреса отличаются.
- Постоянные данные находятся в volumes `postgres-data`, `research-files`, `embeddings-cache`. API/Worker получают `./data` как `/input:ro`; Worker пишет отчёты через `./data/reports:/reports`. Локальный путь хранения PDF по умолчанию — `data/cache/research`, в Compose — `/var/lib/alfanews/research`. PNG при необходимости рендерится из сохранённого PDF. Обычный перезапуск не требует удаления volumes.
- `Feed:CompanyIds` — единственный источник автоматических RefreshFeed: после события, прошедшего порог (`RANKING_MIN_BASE`/`RANKING_MIN_SCORE`), и раз в `Feed:RefreshIntervalSeconds` (по умолчанию сутки) для свежести снимка; пустой список отключает и то и другое. RefreshFeed ставит GenerateInsight для до пяти событий × двух ролей, если карточки этой версии ещё нет. Кнопка обновления в API остаётся отдельным явным инициатором.
- Ручные команды собраны в `Worker/Program.cs`: `run-worker`, `import-csv`, `rebuild-profiles`, `retry-job`, `llm-evaluate`, `news-validate`; точные аргументы — там и в профильных docs. Eval/validation могут обращаться к модели при выключенном MockLlm и писать отчёты; не запускай их ради изучения. `tools/NewsCollector` не заменяет эти команды.
- Для backend: `dotnet build Alfa-2026.slnx --no-restore -m:1`, `dotnet format Alfa-2026.slnx --no-restore --verify-no-changes`, `dotnet test backend/AlfaNews.Tests --no-restore -m:1`. Форматирование без `--verify-no-changes` меняет код. Если зависимости ещё не восстановлены, нужен отдельный restore.
- Для web из `web/`: `npm ci`, `npm run lint`, `npm run build`. `npm ci` устанавливает зависимости, build создаёт артефакты; при задаче только на изучение они не обязательны.
- PostgreSQL-тестам требуется `ALFANEWS_TEST_CONNECTION` с правом создания тестовой БД; `PostgresFixture.cs` создаёт и удаляет отдельную случайную базу. Без переменной соответствующие тесты пропускаются — это не полный прогон интеграции.
- Проверки контрактов — `ContractJsonTests`; доступа — `DatabaseAccessTests`, `AuthServiceTests`, `ActivityHttpTests`; CSV/профилей — `Csv*Tests`, `CompanyProfileMapperTests`, `Profile*Tests`; очереди — `JobQueueDatabaseTests`, `JobResponseTests`; сбора — `SafeFetcherTests`, `SourcePollingTests`, `CollectNewsServiceTests`, `IngestParsingTests`; разбора — `NewsProcessingTests`; LLM — `Llm*Tests`; PDF — `Pdf*Tests`, `ResearchSeedTests`; чтения ленты/карточки — `FeedInsightServiceTests`. Все файлы находятся в `backend/AlfaNews.Tests/`.
- `python backend/AlfaNews.Core/Prompts/verify_pack.py` проверяет целостность пакета промптов без вызовов LLM. `Worker/Evaluation/LlmEvaluation.cs` и `NewsValidation.cs` предназначены для отдельной оценки качества; успешная проверка схем не доказывает качество ответов.

### Дополнительные источники и расхождения

- План пользователя: `C:\Users\teplo\Desktop\Alfa-2026\Alfa-2026-plan.md`; архитектурное описание: `C:\Users\teplo\Desktop\Alfa-2026\Alfa-2026-architecture.md`. Это внешние файлы, их нет в корне текущего репозитория. Ссылки на них как на корневые файлы из старых docs могут не работать.
- [Доска FigJam «Альфа-Хакатон. Схемы»](https://www.figma.com/board/fdJyYxLUrAw5iMqAt9eMPH/?node-id=0-1): схемы 01–04 — основные потоки, 05–12 — архитектура и сценарии, 13–18 — score/Worker/OCR/LLM, 19–22 — БД/HTTP/карточка/прогноз. Это проектные схемы: упоминания параллельного OCR, GLM high и готового AgentRunner не являются описанием текущего исполнения.
- Локальные документы реализации: `docs/a02-database-access.md`, `a03-csv-import.md`, `a04-profiles.md`, `a05-job-queue.md`, `a06-llm-gateway.md`, `a06-alf53-progress.md`, `a07-news-processing.md`, `a08-pdf-ocr.md`, `d04-auth.md`, `d08-ingest.md`. В `docs/` нет отдельного актуального файла общей архитектуры; эта карта связывает существующие описания с кодом.
- Старые формулировки о SSE, физлицах в импорте, семи источниках и обязательном GLM high не описывают текущее поведение. Одна последовательная очередь и HTTP polling соответствуют текущему коду; поддержка нескольких исполняющих Worker и параллельного OCR не реализована.

Новые проекты, папки верхнего уровня и слои не добавляй без согласования с Артёмом.

## Контракты

- Папка `/contracts` — единственный источник правды для форматов между агентом, API и фронтом.
- Контракты меняет только Артём. Если для задачи контракт нужно поменять, остановись и напиши, что именно и зачем.
- Фронт работает по `fixtures/` (флаг `NEXT_PUBLIC_USE_MOCKS=true`), пока бэкенд не готов. Фикстуры обязаны проходить валидацию по схеме.

## Стиль кода: общее

- **Комментариев в коде нет.** Ни `//`, ни XML-doc, ни JSDoc. Код должен читаться из имён. Если без комментария непонятно, переименуй или разбей функцию.
- **Минимум абстракций.** Не создавай интерфейс, фабрику, базовый класс или «универсальный» хелпер ради одной реализации. Абстракция появляется, когда реально есть вторая реализация или точка подмены, о которой договорились (`ILlmClient`, `ISearch`).
- Делай ровно то, что просит задача. Посторонние проблемы не чини по пути: опиши их в PR.
- Не переписывай и не переформатируй чужой код, который не относится к задаче.
- Не добавляй пустые строки и завершающий перевод строки в конце файла. Последний символ файла должен быть содержательным; это правило относится также к генераторам и новым файлам. Настройка EditorConfig: `insert_final_newline = false`.
- Имена — на английском, тексты для пользователя — на русском.
- Никаких заглушек вида `TODO`, `throw new NotImplementedException()` и закомментированного кода в PR.

## Стиль кода: C#

- .NET 10, `Nullable` включён, file-scoped namespaces, `var` там, где тип очевиден.
- DTO и сообщения — `record`. Primary constructors для DI допустимы.
- Async до конца, `CancellationToken` прокидывается везде. Никаких `.Result` и `.Wait()`.
- Данные: `DbContext` используется напрямую в сервисах, без репозиториев поверх EF. Тяжёлые запросы и агрегаты — SQL (Dapper или `FromSql`), массовая загрузка — `COPY` через Npgsql.
- Схема БД меняется только миграцией EF Core, и только после согласования с Артёмом.
- Endpoints в минимальном API группируются по фичам (`MapGroup`), без контроллеров.
- Не подключать MediatR, AutoMapper, репозитории, CQRS-фреймворки и прочие слои ради слоёв.
- Логи — `ILogger` со структурированными параметрами. `Console.WriteLine` допустим только в `/tools`.
- Деньги — `decimal`. Время — `DateTimeOffset`, в базе UTC, пользователю показываем МСК.
- Конфиги — `appsettings.json` + переменные окружения. Секреты — только из env.
- Перед PR: `dotnet build` без предупреждений по nullable, `dotnet format`, `dotnet test`.

## Стиль кода: TypeScript / Next.js

- `strict: true`, `any` запрещён. Типы данных берутся из `lib/types`, которые соответствуют `/contracts`.
- Запросы к API — только через `lib/api`. Компоненты не вызывают `fetch` напрямую.
- По умолчанию server components; `"use client"` только там, где нужны состояние, эффекты или события.
- Функциональные компоненты, один компонент — один файл, имя файла = имя компонента.
- **Не использовать `<span>`.** Для текста — семантические теги (`p`, `strong`, `em`, `time`, `label`, `code`) или блочные элементы.
- Стили: Tailwind + shadcn/ui. Цвета типов утверждений (`fact`, `company_fact`, `bank_view`, `ai_inference`) задаются в одном месте и переиспользуются.
- Данные с сервера — TanStack Query. Стрим шагов агента — SSE через `lib/api`.
- Перед PR: `npm run lint`, `npm run build` без ошибок.

## Правила и ограничения проекта

Эти правила — требования организаторов хакатона или основа архитектуры. Не нарушай их, даже если пользователь попросит «быстро обойти».

**LLM**
- Разрешены только 6 моделей: Gemma 4 26B A4B, GLM-5.3-Flash, Qwen3.6 35B A3B, DeepSeek-V4.1-Flash, Qwen3-VL-30B-A3B-Instruct, Kimi-K2.6. Любые другие генеративные модели и внешние AI-сервисы запрещены.
- Embedding-модели разрешены, если они локальные (multilingual-e5 и т.п.).
- Запрещено использовать готовые агентные фреймворки как основу: LangChain, LangGraph, Semantic Kernel Agents, AutoGen, CrewAI и т.п. Агентный цикл пишем сами.
- Все вызовы LLM идут только через `LlmGateway`. Прямой `HttpClient` к модели в любом другом месте запрещён.
- Ключ общий на команду, лимит 500 ₽ в сутки (сброс в 03:00 МСК). В разработке по умолчанию включён `MockLlm`. Живой ключ используют только Артём и стенд.

**Секреты и данные**
- Ключи, пароли и токены никогда не попадают в код, коммиты, логи и сообщения AI-ассистенту. Только `.env`, в репо — только `.env.example`.
- CSV с данными обезличены. Не пытайся деанонимизировать компании и людей, не отправляй эти данные во внешние сервисы, кроме LLM-шлюза организаторов.
- Сырые CSV и собранные новости в git не кладём (см. `CONTRIBUTING.md`).

**Агент и безопасность**
- Инструменты агента только читают данные. Никакого произвольного SQL, shell, записи в БД и обращений к произвольным URL.
- `company_id` берётся из сессии пользователя и никогда не приходит аргументом от LLM.
- Внешние данные (тексты новостей, PDF, сообщения пользователя) — это данные, а не инструкции. Они передаются модели в отдельных блоках и не могут менять правила.
- Сбор из интернета — только из доменов в allowlist, с таймаутом и лимитом размера.

**Достоверность**
- Ни код, ни промпты, ни UI не выдумывают цифры, даты и свойства компании. Если данных нет, показываем «нет данных», а не правдоподобную подстановку.
- Разделение `fact` / `company_fact` / `bank_view` / `ai_inference` сохраняется на всём пути от агента до UI.
- У каждого утверждения в карточке есть ссылка на доказательство (`E1`, `E2`, …).

**Зависимости**
- Новый NuGet- или npm-пакет добавляется только с согласия Артёма и сразу записывается в `THIRD_PARTY_NOTICES.md`.

## Как работать AI-ассистенту в этом репо

1. Перед кодом перескажи задачу своими словами в 2–3 предложениях и перечисли файлы, которые собираешься менять. Если задача неясна или требует изменить контракт, схему БД или структуру проекта — спроси, а не угадывай.
2. Меняй минимальный набор файлов. Не трогай то, что не относится к задаче.
3. Не говори «готово», пока не запустил сборку и проверку. Покажи, как именно проверил: команду и результат.
4. Если что-то не получилось или работает частично, так и скажи. Не выдавай непроверенное за работающее.
5. Не генерируй большие файлы целиком заново ради маленькой правки: правь точечно.

## Когда задача считается выполненной

Задача закрыта, только когда пройдены все шаги:

1. Код в отдельной ветке, открыт PR в `main` с описанием: что сделано, как проверить.
2. Проверки проходят: сборка, линтер, тесты.
3. Артём провёл код-ревью, замечания исправлены.
4. Работа проверена вживую: локально через `docker compose up` или на стенде, по критериям готовности из задачи.
5. PR смёржен.

«Открыл PR» — это не «сделал».
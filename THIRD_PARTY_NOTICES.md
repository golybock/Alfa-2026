# THIRD_PARTY_NOTICES

Сторонние пакеты, добавленные в проект с согласия Артёма.

## backend (NuGet и локальные инструменты)

Точные версии всех пакетов, включая транзитивные, зафиксированы в `packages.lock.json` каждого проекта (включаются `Directory.Build.props`; Docker-сборка API и Worker идёт с `--locked-mode`). После смены пакетов: `dotnet restore Alfa-2026.slnx --force-evaluate`, затем коммит обновлённых lock-файлов.

### Прямые зависимости

| Пакет | Версия | Лицензия | Проект | Зачем |
|---|---|---|---|---|
| JsonSchema.Net | 7.3.4 | MIT | Core | Проверка ответов LLM по JSON Schema Draft 2020-12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | Core | EF Core provider для PostgreSQL |
| Pgvector.EntityFrameworkCore | 0.3.0 | MIT | Core | Тип vector и запросы EF Core |
| Microsoft.EntityFrameworkCore.Design | 10.0.4 | MIT | Core (PrivateAssets=all) | Генерация миграций и snapshot |
| BCrypt.Net-Next | 4.2.0 | MIT | Core | Хэширование паролей пользователей (D04) |
| AngleSharp | 1.8.5 | MIT | Core | Разбор HTML статей новостных источников (D08) |
| SmartReader | 0.11.1 | Apache-2.0 | Core | Выделение основного текста статьи без меню и рекламы (D08) |
| Microsoft.AspNetCore.OpenApi | 10.0.1 | MIT | Api | Генерация OpenAPI-описания |
| Microsoft.OpenApi | 2.7.5 | MIT | Api | Исправление CVE-2026-49451 в транзитивной зависимости ASP.NET OpenAPI |
| [Swashbuckle.AspNetCore.SwaggerUI](https://www.nuget.org/packages/Swashbuckle.AspNetCore.SwaggerUI/10.1.7) | 10.1.7 | MIT | Api | Встроенный Swagger UI для существующего OpenAPI API |
| [Swagger UI](https://github.com/swagger-api/swagger-ui/blob/master/LICENSE) | JS/CSS в составе Swashbuckle.AspNetCore.SwaggerUI 10.1.7 | Apache-2.0 | Api | Браузерный интерфейс просмотра и вызова API |
| Microsoft.Extensions.Hosting | 10.0.1 | MIT | Worker | Хост фоновых задач Worker |
| Microsoft.DotNet.ILCompiler | 10.0.1 | MIT | NewsCollector | Native AOT-публикация утилиты |
| Microsoft.NET.ILLink.Tasks | 10.0.1 | MIT | NewsCollector | Обрезка при AOT-публикации |
| dotnet-ef | 10.0.4 | MIT | `.config/dotnet-tools.json` | Локальный инструмент миграций |

### Только тесты (AlfaNews.Tests, в образы не попадают)

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| xunit (+ assert, core, extensibility, abstractions, analyzers) | 2.9.3 (analyzers 1.18.0, abstractions 2.0.3) | Apache-2.0 | Фреймворк тестов |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | Запуск тестов в IDE и `dotnet test` |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Тестовый хост (Microsoft.TestPlatform.* и Microsoft.CodeCoverage 17.14.1 — транзитивно) |
| coverlet.collector | 6.0.4 | MIT | Сбор покрытия кода |

### Транзитивные зависимости (основные)

| Пакет | Версия | Лицензия | Откуда |
|---|---|---|---|
| JsonPointer.Net | 5.3.1 | MIT | JsonSchema.Net |
| Json.More.Net | 2.1.1 | MIT | JsonSchema.Net |
| Humanizer.Core | 2.14.1 | MIT | EF Core |
| Npgsql | 10.0.3 | PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL |
| Pgvector | 0.3.2 | MIT | Pgvector.EntityFrameworkCore |
| Microsoft.EntityFrameworkCore / Relational / Abstractions / Analyzers | 10.0.4 | MIT | ORM, конфигурации и миграции |
| Microsoft.Extensions.* (Caching, Configuration, DependencyInjection, Logging, Options, Primitives, Hosting и др.) | 10.0.1–10.0.4 | MIT | ASP.NET Core / Generic Host |
| System.Diagnostics.EventLog | 10.0.1 | MIT | Microsoft.Extensions.Logging.EventLog |
| Newtonsoft.Json | 13.0.3 | MIT | SmartReader |
| Microsoft.CodeAnalysis.* 5.0.0, Microsoft.Build.Framework 18.0.2, Mono.TextTemplating 3.0.0, System.Composition.* 9.0.0, Microsoft.Extensions.DependencyModel 10.0.4 | см. lock-файл | MIT | Только Design-time: Microsoft.EntityFrameworkCore.Design (в рантайм не попадает) |

### Системные пакеты в образах

| Пакет | Версия | Лицензия | Зачем |
|---|---|---|---|
| Poppler: pdfinfo, pdftoppm (poppler-utils) | пакет Ubuntu в базовом образе; проверен 24.02.0-1ubuntu9.11 | GPL-2 или GPL-3; подробности в /usr/share/doc/poppler-utils/copyright | Проверка PDF и рендер страниц в API/Worker (запускается как отдельный процесс) |
| curl | пакет Ubuntu в базовом образе API | curl (MIT-подобная) | Healthcheck контейнера API |

Базовые образы (точные теги): `mcr.microsoft.com/dotnet/aspnet:10.0.13`, `dotnet/runtime:10.0.13`, `dotnet/sdk:10.0.402` (MIT), `node:24.22.1-bookworm-slim` (MIT; Node.js), `pgvector/pgvector:0.8.7-pg17` (PostgreSQL 17; расширение pgvector 0.8.7 — PostgreSQL License), `nginx:1.30.6-alpine` (BSD-2-Clause), `ghcr.io/huggingface/text-embeddings-inference:cpu-1.9.4` (Apache-2.0).

## web (npm)

Версии всех пакетов (439 в дереве) зафиксированы в `web/package-lock.json`, установка в Docker — `npm ci`. Лицензии дерева по lock-файлу: в основном MIT (352), Apache-2.0 (34), ISC (17), BSD, 0BSD, MPL-2.0 (13) и др.

### Прямые зависимости

| Пакет | Версия в lock | Лицензия | Зачем |
|---|---|---|---|
| next | 16.4.2 | MIT | Фреймворк фронтенда |
| react, react-dom | 19.3.0 | MIT | UI |
| @tanstack/react-query | 5.104.5 | MIT | Запросы к API и кэш данных на фронте, единый Query provider |
| @tailwindcss/turbopack | 4.3.3 | MIT | Интеграция Tailwind CSS с Turbopack (dev) |
| tailwindcss | 4.3.3 | MIT | Стили (dev) |
| typescript | 5.9.3 | Apache-2.0 | Типизация (dev) |
| eslint, eslint-config-next | 9.39.5, 16.4.2 | MIT | Линтер (dev) |
| @types/node, @types/react, @types/react-dom | 20.19.43, 19.3.0, 19.3.0 | MIT | Типы (dev) |

### Транзитивные зависимости с особыми лицензиями

| Пакет | Лицензия | Откуда / примечание |
|---|---|---|
| @tanstack/query-core | MIT | Ядро @tanstack/react-query |
| lightningcss и платформенные сборки | MPL-2.0 | Tailwind CSS; используется без изменений исходников |
| @img/sharp-libvips-* | LGPL-3.0-or-later | Необязательные бинарные зависимости sharp (Next.js, оптимизация изображений); линкуются динамически, без изменений |
| @img/sharp-* | Apache-2.0 (wasm32: Apache-2.0 AND LGPL-3.0-or-later AND MIT) | sharp |
| caniuse-lite | CC-BY-4.0 | Данные браузерной поддержки (browserslist) |
| argparse | Python-2.0 | Зависимость eslint (js-yaml) |
| axe-core | MPL-2.0 | eslint-plugin-jsx-a11y |

## Локальные embeddings

| Компонент | Версия | Лицензия | Зачем |
|---|---|---|---|
| [Text Embeddings Inference](https://github.com/huggingface/text-embeddings-inference/blob/main/LICENSE) | Docker-образ cpu-1.9.4 | Apache-2.0 | Локальный HTTP-сервис e5; фильтр новостей и поиск похожих публикаций A07 |
| [intfloat/multilingual-e5-base](https://huggingface.co/intfloat/multilingual-e5-base) | revision d128750597153bb5987e10b1c3493a34e5a4502a, существующий ONNX int8 | MIT | Локальные векторы размерности 768 для новостей; веса в Docker volume, не в Git |

## Генеративные модели

Используются только модели из разрешённого списка кейса, через шлюз организаторов хакатона (OpenAI-совместимый API). Веса моделей в проект не входят и не распространяются; условия использования — условия шлюза кейса.

| Модель | Имя в шлюзе | Задачи |
|---|---|---|
| DeepSeek-V4.1-Flash | `deepseek/deepseek-v4.1-flash` | фильтр заголовков, разбор новости, агент, починка, чат, проверка темы вопроса |
| GLM-5.3-Flash | `z-ai/glm-5.3-flash` | критик карточки, OCR страниц PDF |

Остальные модели списка (`google/gemma-4-26b-a4b-it`, `qwen/qwen3.6-35b-a3b`, `qwen/qwen3-vl-30b-a3b-instruct`, `moonshotai/kimi-k2.6`) разрешены настройкой, но по умолчанию не используются. Эмбеддинг-модель e5 — локальная, см. выше.

## Данные кейса

| Данные | Источник | Как используются |
|---|---|---|
| `data/df_Uid_data_hack.csv`, `data/df_Uid_payment_hack.csv`, `data/df_Fid_Uid_link_hack.csv` | организаторы хакатона Альфа-Банка, обезличенные данные | компании, платежи, профили и учётки демо-компаний; связи физлиц (`df_Fid_Uid_link_hack.csv`) не импортируются. Деанонимизация не выполняется, данные передаются только в шлюз модели кейса |
| девять PDF аналитики Альфа-Банка в `data/` | материалы кейса, © АО «Альфа-Банк» | распознаны GLM-5.3-Flash, текст и страницы показываются как «Позиция Альфа-Банка» с датой обзора |

## Источники новостей

Новости собираются из публичных RSS-лент; в интерфейсе показываются короткие цитаты со ссылкой на первоисточник, права на тексты принадлежат издателям. Полные тексты хранятся только в локальной базе стенда для разбора. Каталоги — `data/sources.json`, `data/sources-specialized.json`.

| Источник | Лента | Статус |
|---|---|---|
| РБК | rssexport.rbc.ru | включён |
| Минфин России | minfin.gov.ru | включён |
| Правительство РФ | government.ru | включён |
| Банк России, пресс-релизы | cbr.ru | включён |
| ФНС России | nalog.gov.ru | выключен |
| Коммерсантъ: экономика, финансы | kommersant.ru | включён |
| Ведомости: экономика, финансы, бизнес | vedomosti.ru | включён |

## Смоделированные интеграции

Эти части имитируют окружение банка для демонстрации и не подключены к реальным системам Альфа-Банка:

- **Интерфейс Альфа-Бизнеса** — главная страница, меню и разделы-заглушки (`web/src/components/bank/`, `web/fixtures/bank-home.json`); логотип `web/public/alfa-logo.jpg` и фирменные цвета используются только в демонстрационном макете для хакатона.
- **Вход** — собственные демо-учётки с паролем `password` (`UserSeedService`), не Alfa ID и не банковская авторизация.
- **«Связаться с менеджером»** — записывает действие в журнал `user_activity`; заявка менеджеру не отправляется, интерфейс сообщает об этом.
- **Банковские продукты и данные клиента** — берутся из CSV кейса, а не из систем банка.
# Alfa-2026

Локальный запуск в PowerShell:

```powershell
Copy-Item .env.example .env
```

В локальном `.env` замените `POSTGRES_PASSWORD` на свой пароль. `POSTGRES_USER` и `POSTGRES_DB` можно оставить как в примере. Не публикуйте `.env`. Это учётные данные PostgreSQL, а не логин пользователя сайта. По умолчанию выключён MockLlm; ключ модели для запуска нужен.

```powershell
docker compose up -d --build
```

Сайт: [localhost:8080](http://localhost:8080). PostgreSQL доступен локально по `127.0.0.1:15432`; имя БД, пользователь и пароль — из `.env` (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`). Внутри Compose используется `postgres:5432`. При первом запуске PostgreSQL создаёт пользователя и БД из `.env`, сервис `migrate` применяет EF-миграции и завершается с кодом 0, затем запускаются API и Worker. Worker автоматически импортирует компании и платежи из CSV в `data`; завершённый пакет при перезапуске пропускается. Отчёт очистки: `data/reports/import_report.json`. [Правила импорта и ручной запуск](docs/a03-csv-import.md). Данные и PDF сохраняются в volumes. Первая загрузка локальной embedding-модели требует интернета и времени.

Существующий PostgreSQL volume сохраняет прежний пароль: изменение `.env` не меняет пароль уже созданного пользователя. Не удаляйте volume для обычного обновления приложения.

Модель данных и общий контроль доступа подготовлены в A02; логин, logout и `/me` реализованы в D04: [вход и учётки](docs/d04-auth.md). Worker собирает новости из RSS семи источников: [сбор новостей](docs/d08-ingest.md). [Подключение БД, доступа и проверки](docs/a02-database-access.md). После импорта Worker автоматически строит `company_profile` и `group_profile`; актуальные профили при перезапуске пропускаются. Три выбранные демо-компании и черновики каналов для K04: `data/reports/profile_candidates.json`. [Расчёты профилей и ручная пересборка](docs/a04-profiles.md).

## Вход на сайт

Учётки создаёт Worker при первом запуске, сразу после импорта CSV (обычно в первые минуты); до этого вход отвечает «Данные компаний ещё загружаются». Пароль у всех — `password`.

| Логин | Пароль | Кто | Доступ |
| --- | --- | --- | --- |
| `admin` | `password` | оператор | без доступа к данным компаний; операторские endpoints (задания, PDF, проверка страниц) |
| `operator` | `password` | оператор | то же, что `admin` |
| `demo` | `password` | ЛПР | все компании из `FEED_COMPANY_IDS`, у каждой роли CEO и CFO; после входа выбирает организацию и роль |
| `U_68157` | `password` | ЛПР | только эта компания, роли CEO и CFO |
| `U_71942` | `password` | ЛПР | только эта компания, роли CEO и CFO |
| `U_258515` | `password` | ЛПР | только эта компания, роли CEO и CFO |

Учётки по id компании создаются для каждой компании из `FEED_COMPANY_IDS` в `.env`. Каждый перезапуск Worker сбрасывает пароли этих учёток на `password`. Подробнее — [вход и учётки](docs/d04-auth.md).

## Переменные окружения

Скопируйте `.env.example` в `.env`. В примере только основные переменные; остальные необязательны, у них есть значения по умолчанию в `compose.yaml` и коде. Чтобы изменить, добавьте строку в `.env`.

### Основные (есть в `.env.example`)

| Переменная | Назначение |
| --- | --- |
| `HTTP_PORT` | Порт сайта (по умолчанию 8080). |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | База и пользователь PostgreSQL. Значения в `.env.example` фиксированные, менять их для проверки не нужно; пароль задаётся при первом создании volume. |
| `LLM_BASE_URL`, `LLM_API_KEY` | Адрес шлюза модели и ключ для живой модели. |
| `FEED_COMPANY_IDS` | Компании ленты; для каждой Worker создаёт учётку с логином = id компании. |
| `LLM_DAILY_LIMIT_RUB` | Дневной лимит расходов стенда на модель, ₽ (сутки с 03:00 МСК). Задания сверх лимита завершаются `budget_exhausted` и повторяются вручную. Ключ общий на команду: Worker дополнительно сверяется с расходом и лимитом ключа на шлюзе (`/key/info`). |

### Дополнительные

| Переменная | По умолчанию | Назначение |
| --- | --- | --- |
| `LLM_USE_MOCK` | `false` | `false` — живая модель (нужны `LLM_BASE_URL` и `LLM_API_KEY`); `true` — модель не вызывается (MockLlm), ключ не нужен. |
| `PUBLIC_ORIGIN` | `http://localhost:HTTP_PORT` | Внешний адрес сайта за шлюзом; с него API принимает изменяющие запросы (Origin). |
| `LLM_TRIAGE_MODEL` | `deepseek/deepseek-v4.1-flash` | Модель фильтра заголовков новостей. |
| `LLM_EXTRACT_MODEL` | `deepseek/deepseek-v4.1-flash` | Разбор новости в факты. |
| `LLM_AGENT_MODEL` | `deepseek/deepseek-v4.1-flash` | Агент карточки: план и черновик. |
| `LLM_REPAIR_MODEL` | `deepseek/deepseek-v4.1-flash` | Починка ответов (кроме фильтра и разбора — они чинятся своей моделью). |
| `LLM_CRITIC_MODEL` | `z-ai/glm-5.3-flash` | Критик карточки. |
| `LLM_OCR_MODEL` | `z-ai/glm-5.3-flash` | Распознавание PDF (нужна модель, принимающая изображения). |
| `LLM_CHAT_MODEL` | `deepseek/deepseek-v4.1-flash` | Ответы чата по карточке. |
| `LLM_SCOPE_GATE_MODEL` | `deepseek/deepseek-v4.1-flash` | Проверка, что вопрос чата по теме. |
| `LLM_THINKING_ENABLED` | `true` | Рассуждения включены у всех задач, кроме фильтра заголовков; у GLM-5.3-Flash они принудительные. |
| `LLM_EXTRACT_THINKING_ENABLED` | `true` | Рассуждения при разборе новости. |
| `LLM_GLM_REASONING_LEVEL` | `low` | Уровень рассуждений GLM. |
| `LLM_STRUCTURED_OUTPUT` | `true` | Структурированный ответ модели (`response_format`). |
| `CRITIC_ENABLED` | `true` | Включить критика карточки. |
| `CRITIC_SCOPE` | `all_full` | На каких карточках работает критик. |
| `FEED_MODE` | `live` | `live` — живая лента; `demo` — снимок, заранее посчитанный командой Worker `precompute-demo`. |
| `FEED_REFRESH_INTERVAL_SECONDS` | `86400` | Интервал обновления ленты. |
| `RANKING_MIN_BASE` | `0.30` | Порог базовой оценки события для ленты. |
| `RANKING_MIN_SCORE` | `0.15` | Порог итоговой оценки события для ленты. |
| `QUEUE_NEWS_SLOTS` | `4` | Исполнители полосы новостей (1–16). |
| `QUEUE_DOCUMENTS_SLOTS` | `3` | Исполнители полосы документов (1–16). |
| `QUEUE_INSIGHTS_SLOTS` | `2` | Исполнители полосы карточек (1–16). |
| `QUEUE_CHAT_SLOTS` | `2` | Исполнители полосы чата (1–16). |
| `LLM_MAX_PARALLEL_REQUESTS` | `8` | Общий предел одновременных запросов к модели (1–16). |
| `LLM_DEEPSEEK_INPUT_ESTIMATE_RUB_PER_MILLION` | `55` | Оценка цены входа DeepSeek для резерва бюджета до ответа, ₽ за 1 млн токенов. |
| `LLM_DEEPSEEK_OUTPUT_ESTIMATE_RUB_PER_MILLION` | `100` | То же для выхода DeepSeek. |
| `LLM_GLM_INPUT_ESTIMATE_RUB_PER_MILLION` | `22` | То же для входа GLM. |
| `LLM_GLM_OUTPUT_ESTIMATE_RUB_PER_MILLION` | `50` | То же для выхода GLM. |
| `POSTGRES_HOST`, `POSTGRES_PORT` | `127.0.0.1`, `15432` | Только для запуска API и Worker без Docker (`dotnet run`): адрес PostgreSQL на хосте. |
| `Embeddings__BaseUrl` | `http://127.0.0.1:18081` | Только без Docker: адрес сервиса векторов на хосте. |

Модели задаются полными именами шлюза. Разрешены: `deepseek/deepseek-v4.1-flash`, `z-ai/glm-5.3-flash`, `google/gemma-4-26b-a4b-it`, `qwen/qwen3.6-35b-a3b`, `qwen/qwen3-vl-30b-a3b-instruct`, `moonshotai/kimi-k2.6`; другое Worker не запустит. Полоса `data` всегда последовательная; карточки CEO и CFO одного события не идут одновременно: вторая ждёт общий план.

## Пересборка с нуля

Из корня репозитория, без удаления данных PostgreSQL, PDF и загруженной модели:

```powershell
docker compose -p alfa-news -f compose.yaml down
docker compose -p alfa-news -f compose.yaml build --no-cache
docker compose -p alfa-news -f compose.yaml up -d
```

`down` удаляет контейнеры и сеть только проекта alfa-news. `--no-cache` пересобирает слои без использования прежнего кэша.

Чтобы дополнительно освободить место от всего неиспользуемого кэша сборки Docker:

```powershell
docker builder prune --all
```

Команда запросит подтверждение и затронет общий кэш сборки всех проектов. Контейнеры и volumes она не удаляет. Для обычной чистой пересборки проекта достаточно `build --no-cache`; `docker system prune` и удаление volumes не нужны.

## Зависимости web

`web/package-lock.json` хранится в Git. Для установки используйте `npm ci` в каталоге web; Docker использует ту же команду. Версии Next и eslint-config-next согласованы и остаются 16.4.2.

Пять high-предупреждений npm audit относятся к одной цепочке ESLint: eslint-config-next → fast-glob → micromatch → braces. Для braces 3.0.3 исправления CVE-2026-93687 пока нет. Не применять `npm audit fix --force`: предлагаемый откат eslint-config-next до 14.x не принят. После выхода исправления обновить цепочку, повторить audit, lint и build.

Локальные API, Worker и EF автоматически читают существующий `.env` из корня репозитория. Переменные процесса имеют приоритет; секреты в вывод не попадают. Настройка LLM, бюджет и проверка промптов: [A06 и ALF-53](docs/a06-llm-gateway.md).
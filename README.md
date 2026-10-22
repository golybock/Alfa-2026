# Alfa-2026

Локальный запуск в PowerShell:

```powershell
Copy-Item .env.example .env
```

В локальном `.env` замените `POSTGRES_PASSWORD` на свой пароль. `POSTGRES_USER` и `POSTGRES_DB` можно оставить как в примере. Не публикуйте `.env`. Это учётные данные PostgreSQL, а не логин пользователя сайта. По умолчанию включён MockLlm; ключ модели для запуска не нужен.

```powershell
docker compose up -d --build
```

Сайт: [localhost:8080](http://localhost:8080). PostgreSQL доступен локально по `127.0.0.1:15432`; имя БД, пользователь и пароль — из `.env` (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`). Внутри Compose используется `postgres:5432`. При первом запуске PostgreSQL создаёт пользователя и БД из `.env`, сервис `migrate` применяет EF-миграции и завершается с кодом 0, затем запускаются API и Worker. Worker автоматически импортирует компании и платежи из CSV в `data`; завершённый пакет при перезапуске пропускается. Отчёт очистки: `data/reports/import_report.json`. [Правила импорта и ручной запуск](docs/a03-csv-import.md). Данные и PDF сохраняются в volumes. Первая загрузка локальной embedding-модели требует интернета и времени.

Существующий PostgreSQL volume сохраняет прежний пароль: изменение `.env` не меняет пароль уже созданного пользователя. Не удаляйте volume для обычного обновления приложения.

Модель данных и общий контроль доступа подготовлены в A02; логин, logout и `/me` реализованы в D04: [вход и учётки](docs/d04-auth.md). Worker собирает новости из RSS семи источников: [сбор новостей](docs/d08-ingest.md). [Подключение БД, доступа и проверки](docs/a02-database-access.md). После импорта Worker автоматически строит `company_profile` и `group_profile`; актуальные профили при перезапуске пропускаются. Три выбранные демо-компании и черновики каналов для K04: `data/reports/profile_candidates.json`. [Расчёты профилей и ручная пересборка](docs/a04-profiles.md).

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
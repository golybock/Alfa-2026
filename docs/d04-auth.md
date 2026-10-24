# Вход, сессия и /me (D04)

## Endpoints

| Метод и путь | Ответ | Ошибки |
| --- | --- | --- |
| POST /api/auth/login | 200 MeResponse и cookie `alfa_session` | 400 пустые поля, 401 неверный логин или пароль, неактивный пользователь, 503 `data_not_ready` до импорта компаний |
| GET /api/me | 200 MeResponse: только доступные компании CLIENT и роли ceo/cfo | 401 |
| POST /api/auth/logout | 204, сессия отозвана, cookie удалена | 401 |

Не реализовано: ограничение частоты входа (429 из `contracts/http.md`).

## Cookie и сессия

- Токен — 32 случайных байта, в базе только SHA-256 (`auth_session.token_hash`). Пароль — bcrypt (`app_user.password_hash`).
- Cookie: `HttpOnly`, `SameSite=Lax`, `Secure` при HTTPS (в том числе `X-Forwarded-Proto: https` за reverse proxy), срок равен сроку сессии.
- Срок сессии: `Auth:SessionHours`, по умолчанию 12.
- Изменяющие запросы проверяются по `Origin` из `Auth:AllowedOrigins`. В Development разрешён `http://localhost:3000`; за reverse proxy задайте внешний origin явно (`Auth__AllowedOrigins__0`). В Compose API принимает `PUBLIC_ORIGIN` из `.env` (по умолчанию `http://localhost:HTTP_PORT`) и `http://127.0.0.1:HTTP_PORT`: без этого вход через шлюз отклонялся как `invalid_origin`.

## Демо-учётки

Учётки создаёт Worker при каждом старте, сразу после импорта CSV (до сборки профилей и очереди). Пароль у всех один и задан в коде: `password` (`UserSeedService.Password`); в `.env` паролей нет.

- Операторы `admin` и `operator`: `is_operator = true`, без доступа к компаниям.
- `demo`: роли ceo и cfo в каждой компании из `Feed__CompanyIds` (`FEED_COMPANY_IDS`).
- По учётке на каждую компанию из `Feed__CompanyIds`: логин — id компании, роли ceo и cfo только в ней.
- Компании, которых нет в `company` с `cl_type = CLIENT`, пропускаются с предупреждением в логе Worker.
- Повторный старт сбрасывает пароль этих учёток на `password` и пересоздаёт их доступы.
- Пока импорт не закончился (в базе нет ни одной компании `CLIENT`), неудачный вход отвечает 503 `data_not_ready` «Данные компаний ещё загружаются», а не 401.

Пароль общий и известный: стенд только для демо и локальной разработки, ограничения частоты входа нет.

## Фронт локально

В `web/.env.local`: `NEXT_PUBLIC_USE_MOCKS=false` и `API_PROXY_URL=http://localhost:5026`. Next проксирует `/api/*` на API; в Compose это делает nginx.

## Пользователи MVP

| Логин | Пароль | Кто | Доступ |
| --- | --- | --- | --- |
| `admin` | `password` | оператор | без доступа к данным компаний; операторские endpoints (задания, PDF, проверка страниц) |
| `operator` | `password` | оператор | то же, что `admin` |
| `demo` | `password` | ЛПР | все компании из `FEED_COMPANY_IDS`, у каждой роли CEO и CFO; после входа выбирает организацию и роль |
| `U_68157` | `password` | ЛПР | только эта компания, роли CEO и CFO |
| `U_71942` | `password` | ЛПР | только эта компания, роли CEO и CFO |
| `U_258515` | `password` | ЛПР | только эта компания, роли CEO и CFO |

Каждая из трёх компаний `demo` принадлежит своей группе (`group_134462_sb`, `group_134546_sb`, `group_134530_sb`); доступ выдаётся к самим компаниям, а не ко всем участникам группы.

## Материалы (25.10)

Загрузка PDF аналитики, список документов и новость по ссылке (`/admin/documents`, `/admin/news`) открыты всем вошедшим, включая учётки отдельных компаний (`AccessService.CanAddMaterials`, в `/me` — `can_add_materials`). Это решение для демонстрации: аналитика банка общая и попадает в карточки всех компаний, поэтому в боевом режиме право нужно оставить операторам. Проверка страниц PDF и повтор заданий — только оператор.

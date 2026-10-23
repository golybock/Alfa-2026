# Вход, сессия и /me (D04)

## Endpoints

| Метод и путь | Ответ | Ошибки |
| --- | --- | --- |
| POST /api/auth/login | 200 MeResponse и cookie `alfa_session` | 400 пустые поля, 401 неверный логин или пароль, неактивный пользователь |
| GET /api/me | 200 MeResponse: только доступные компании CLIENT и роли ceo/cfo | 401 |
| POST /api/auth/logout | 204, сессия отозвана, cookie удалена | 401 |

Не реализовано: ограничение частоты входа (429 из `contracts/http.md`).

## Cookie и сессия

- Токен — 32 случайных байта, в базе только SHA-256 (`auth_session.token_hash`). Пароль — bcrypt (`app_user.password_hash`).
- Cookie: `HttpOnly`, `SameSite=Lax`, `Secure` при HTTPS (в том числе `X-Forwarded-Proto: https` за reverse proxy), срок равен сроку сессии.
- Срок сессии: `Auth:SessionHours`, по умолчанию 12.
- Изменяющие запросы проверяются по `Origin` из `Auth:AllowedOrigins`. В Development разрешён `http://localhost:3000`; за reverse proxy задайте внешний origin явно (`Auth__AllowedOrigins__0`). В Compose API принимает `PUBLIC_ORIGIN` из `.env` (по умолчанию `http://localhost:HTTP_PORT`) и `http://127.0.0.1:HTTP_PORT`: без этого вход через шлюз отклонялся как `invalid_origin`.

## Демо-учётки

Пользователи создаются командой, пароли берутся только из окружения:

```bash
Seed__CompanyIds=ID1,ID2 Seed__UserPassword=... Seed__OperatorPassword=... dotnet run --project backend/AlfaNews.Api -- --seed-users
```

- `demo` (или `Seed__UserLogin`): роли ceo и cfo в каждой компании из `Seed__CompanyIds`.
- `operator` (или `Seed__OperatorLogin`): оператор без доступа к компаниям.
- Компании должны существовать в `company` с `cl_type = CLIENT`; иначе команда завершится ошибкой.
- Повторный запуск обновляет пароли и пересоздаёт доступы этих двух пользователей.

## Фронт локально

В `web/.env.local`: `NEXT_PUBLIC_USE_MOCKS=false` и `API_PROXY_URL=http://localhost:5026`. Next проксирует `/api/*` на API; в Compose это делает nginx.

## Пользователи MVP

В базе ровно два пользователя, оба создаются командой `--seed-users`:

| Логин | Кто | Доступ |
| --- | --- | --- |
| `demo` | ЛПР | компании из `Seed__CompanyIds` (сейчас `U_68157`, `U_71942`, `U_258515`), у каждой роли CEO и CFO через `user_company_access`; после входа выбирает организацию и роль |
| `operator` | админ, `is_operator = true` | без доступа к данным компаний, только операторские endpoints (задания, PDF) |

Каждая из трёх компаний `demo` принадлежит своей группе (`group_134462_sb`, `group_134546_sb`, `group_134530_sb`); доступ выдаётся к самим компаниям, а не ко всем участникам группы.

## Демо-вход по id компании

Если задан `Auth__DemoPassword` (в Compose — `DEMO_LOGIN_PASSWORD` из `.env`), любой id компании `CLIENT` работает как логин с этим общим паролем и открывает эту компанию в ролях CEO и CFO. Для каждой компании создаётся отдельный пользователь `demo:<id>` без права на другие компании. Пока переменная пуста, режим выключен. Включать только в демо и локальной разработке: пароль общий, ограничения частоты входа нет.
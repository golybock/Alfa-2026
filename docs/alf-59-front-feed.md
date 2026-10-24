# ALF-59: лента, демо-элементы, состояния, плавность

Состояние на 24.10.2026. Задача: [ALF-59](https://linear.app/alfa-hackathon/issue/ALF-59/front-prevyu-v-lente-demo-elementy-sostoyaniya-plavnost), статус In Progress. Изменения только в `web/`, контракты и backend не менялись.

## Источники

- Текст задачи в Linear и дополнение Антона (подключить новые поля ленты и включить кнопки).
- Макеты из архива «Альфа Хакатон.zip» (17 PNG). Figma MCP был недоступен из-за лимита тарифа Starter.
- Контракт обновился слиянием с `main` (PR #16): `significance`, `direction`, `insight_headline`, `queued_at`, `retry_available`, `collection_failed`, `last_collected_at`, `label` в причинах, `GET /insights/{id}/steps`. `npm run contracts:sync` не запускал: `web/src/lib/types/api.ts` и `web/fixtures/api` уже совпадают с `contracts/`.

## Что сделано

| Пункт | Что сделано | Файлы |
| --- | --- | --- |
| 1. Значимость | Бейдж «Высокая / Средняя / Низкая значимость» с тремя столбиками в строке ленты, виджете и карточке; при `null` не показывается. Бейдж «№N по значимости» удалён | `components/ui/ImpactBadge.tsx`, `InsightRow.tsx`, `InsightsWidgetRow.tsx`, `InsightCardHeader.tsx`, `InsightCardBody.tsx` |
| 2. Направление | `KindBadge` в строке ленты из `item.direction`, при `null` скрыт. Фильтры «Все / Риски / Возможности» с количеством работают на клиенте по этому же полю | `InsightRow.tsx`, `FeedFilterTabs.tsx`, `lib/feedFilter.ts`, `InsightsFeed.tsx` |
| 3. Заголовок | Крупно `insight_headline ?? headline`, до двух строк. Если есть `insight_headline`, заголовок новости выводится мелко рядом с названием источника | `InsightRow.tsx`, `InsightsWidgetRow.tsx` |
| Тема строки | Верхняя строка превью берётся из `relevance.reasons[0].label` | `InsightRow.tsx` |
| 4. Описание | Описание в ленте и заметка в виджете в одну строку (`line-clamp-1`) | `InsightRow.tsx`, `InsightsWidgetRow.tsx` |
| 5. Шапка листинга | Кнопка «Демо: другой клиент» убрана. У выбора роли убрана подпись «Демо · роль», рамка осталась | `FeedHeader.tsx`, `RoleSwitch.tsx` |
| 6. Кнопки | Оценки «Интересно / Не интересно / Скрыть» включены. «Связаться с менеджером» открывает окно «В демо заявка не отправляется. В рабочей версии она уйдёт вашему менеджеру» и пишет `manager_contact_requested` в `/activity` | `FeedbackButtons.tsx`, `ManagerContactButton.tsx` |
| 7. Состояния карточки в ленте | `queued` и `processing`: «Готовится» и «в очереди с HH:MM, обычно около 3 минут»; `failed` с `retry_available`: кнопка «Повторить» (тот же POST, затем опрос job); без `retry_available`: «Не удалось подготовить карточку» | `PendingAnalysisNote.tsx` |
| 7. Сбор новостей | Баннер «Не удалось получить новые новости» с временем `last_collected_at` при `collection_failed`. Баннер при `refresh_status = failed` оставлен отдельно | `InsightsFeed.tsx` |
| Шаги системы | Статичный список заменён на `GET /api/insights/{id}/steps`: название, пояснение, статус (пропущено / не удалось), длительность и итог. При 404 пишет «Данных нет» | `SystemSteps.tsx`, `WhyPanel.tsx`, `lib/api/*`, `lib/hooks/queries.ts` |
| Критик | Бейдж «Без проверки критиком» в шапке карточки, если статус критика начинается с `skipped_` | `InsightCardHeader.tsx` |

API-слой: в `Api` добавлены `getInsightSteps` и `recordActivity` (HTTP и mock), в mock добавлен сценарий `?mock=collection-failed` на фикстуре `feed-failed.json`.

## Как проверено

- `npm run lint`, `npx tsc --noEmit`, `npm run build`: без ошибок.
- Mock-сборка в браузере: лента на `feed-ready.json` (бейджи, тема, заголовок, фильтры), `?mock=pending` («Готовится… в очереди с 12:00»), `?mock=collection-failed` (баннер и «Повторить»), карточка (окно менеджера, шаги системы из `insight-steps.json`, кнопки оценок активны).
- Живой API (`demo`): лента отдаёт `significance`, `collection_failed`, `last_collected_at`; `POST /activity` отвечает 204; `GET /steps` для старых карточек отвечает 404 (карточки считались до появления трассы).
- Не проверено вживую: кнопка «Повторить» на живом API, живые `direction` и `insight_headline` (в текущей БД у событий их нет, карточки не готовы), `feed-failed` для баннера в live.

## Состояния из п. 7

| Состояние | Результат |
| --- | --- |
| Загрузка и пустая лента | Работает |
| Генерация ленты (`FeedProgress`) | Работает; в макете есть скелетоны карточек под списком шагов, их нет |
| Карточка: готовится, не удалось, повтор | Работает на фикстурах; повтор на живом API не проверен |
| Не удалось получить новые новости | Работает на фикстуре; на живом API `collection_failed = false` |
| Ошибки сети и недоступный API | Есть ветки с сообщением и «Повторить» в ленте; в виджете только сообщение |

## П. 8: анимации и плавность

Всё на CSS и Tailwind, новых пакетов нет.

| Что | Как сделано | Файлы |
| --- | --- | --- |
| Единые длительность и easing | Переменные `--motion-duration: 150ms` и `--motion-ease` в `:root` | `app/globals.css` |
| Ховер на кнопках, ссылках и `summary` | Базовое правило переходов цвета, фона, рамки, тени и прозрачности; нажатие слегка приглушает элемент; курсор «рука» у активных кнопок | `app/globals.css` |
| `focus-visible` | Синяя обводка 2 px со смещением 2 px у всех элементов при навигации с клавиатуры | `app/globals.css` |
| `prefers-reduced-motion` | Анимации и переходы практически отключаются | `app/globals.css` |
| Строки ленты | Плавная тень при наведении | `components/feed/InsightRow.tsx` |
| Переходы между страницами | `template.tsx` в `(bank)`: мягкое появление содержимого (только прозрачность, без сдвигов раскладки); оболочка банка не перерисовывается | `app/(bank)/template.tsx` |
| Скелетоны вместо надписей «Загружаем…» | `FeedSkeleton` для ленты и `CardSkeleton` для карточки на общем `SkeletonBlock` | `components/feed/FeedSkeleton.tsx`, `components/card/CardSkeleton.tsx`, `components/ui/SkeletonBlock.tsx` |
| Предзагрузка карточки | При наведении или фокусе на «Открыть» карточка заранее запрашивается (кеш 30 с), на странице карточки данные уже есть | `components/feed/OpenInsightLink.tsx`, `lib/hooks/queries.ts` |

Проверка в mock-сборке: переходы у кнопок 0,15 с, у строки ленты `box-shadow` 0,15 с, `.page-enter` есть, правило `prefers-reduced-motion` подключено, `:focus-visible` срабатывает по Tab. Скелетоны глазами не видел: mock отвечает мгновенно. Живой стек пересобран (`web`), `lint`, `tsc` и `build` проходят.

## Что осталось

- Баннер «Не удалось получить новые новости» и тексты в макете: сверить вид с Антоном.
- Живая проверка «Повторить» после запуска Worker (по просьбе парсер не запускался).
- Перед PR: `npm run lint`, `npm run build`, PR в `main`, ревью Артёма.

## Замечания

- В БД события `66936499…` заголовок `Фикстура: тестовая ставка` получен из MockLlm (первый запуск стека с `LLM_USE_MOCK=true`). Нужно переразобрать новость живой моделью.
- После слияния PR #16 учётные записи изменились (`demo`, `U_68157` и т.д. описаны в `docs/d04-auth.md`). Мои старые логины с общим `DEMO_LOGIN_PASSWORD` для `U_68157`, `U_71942`, `U_258515` в текущей БД не входят; `demo` входит.
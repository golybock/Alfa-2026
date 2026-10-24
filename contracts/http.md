# HTTP-контракт MVP

DTO: [Api](../backend/AlfaNews.Contracts/Api), [api.ts](api.ts); общие поля карточки в [Domain](../backend/AlfaNews.Contracts/Domain). Пути ниже нормативные для задач Дани. В A02 реализован endpoint activity; остальные реализуются в соответствующих задачах.

Все пути начинаются `/api`. Авторизация — cookie HttpOnly; production cookie Secure/SameSite=Lax, same-origin reverse proxy сайта/API. Изменяющие запросы проверяют Origin/CSRF; API не принимает произвольный cross-origin credentialed вызов. Один общий middleware/сервис доступа пишет Артём/проверяет в A02, Даня применяет его во всех своих endpoints. Пользователь имеет доступ только к CLIENT-компаниям из MeResponse, операторские действия требуют IsOperator. Секреты и raw ORM entities наружу не отдаём.

| Метод / путь | Вход | Успешный ответ | Ошибки / права | Владелец |
| --- | --- | --- | --- | --- |
| POST /auth/login | LoginRequest | 200 MeResponse + cookie | 400/401/429/503 | D04 |
| POST /auth/logout | без тела | 204, отзыв cookie/session | 401 | D04 |
| GET /me | — | 200 MeResponse | 401 | D04 |
| GET /companies/{companyId}/feed?role=cfo | role=ceo/cfo; параметров периода и фильтров нет | 200 FeedResponse | 401/403 | D09 |
| POST /companies/{companyId}/feed/refresh | без тела | 202 AcceptedResponse | 401/403/429 | D09 |
| GET /companies/{companyId}/events/{eventId}/insight?role=cfo | проверенные company/event/role | 200 InsightDto; 202 PendingInsightResponse, если уже готовится | 401/403/404 | D09 |
| POST /companies/{companyId}/events/{eventId}/insight | InsightRequest | 200 InsightDto из готового; иначе 202 AcceptedResponse | 401/403/404/429 | D09 |
| GET /insights/{id}/evidence/{evidenceId} | ID из evidence данной карточки | 200 EvidenceDto | 401/403/404 | D09 |
| GET /research/{documentId}/pages/{pageNo} | ссылка из доступного insight либо оператор | 200 image/png | 401/403/404 | D09 |
| POST /insights/{id}/chat | ChatRequest | 200 ChatAnswerDto из кэша; иначе 202 AcceptedResponse | 400/401/403/409/429 | D09 → A13 |
| GET /insights/{id}/chat | — | 200 ChatHistoryResponse: только история текущего пользователя | 401/403 | D09 |
| POST /insights/{id}/feedback | FeedbackRequest | 204 | 400/401/403 | D12 |
| POST /insights/{id}/activity | UserActivityRequest | 204, включая точный повтор | 400/401/403/404/409; доступ к карточке | A02 |
| GET /insights/{id}/steps | без тела | 200 InsightStepsResponse | 401/403/404; 409 insight_not_prepared | ALF-59 |
| GET /jobs/{id} | — | 200 JobResponse | 401/403/404; только владелец либо оператор | D09 |
| POST /admin/documents | multipart: file PDF; title string; published_on YYYY-MM-DD или пусто; source_url URL или пусто | 202 DocumentAcceptedResponse | 400/401/403/413/415; оператор или `can_add_materials` | D10 |
| GET /admin/documents | — | 200 DocumentListResponse, включая стартовые 9 документов | 401/403; оператор или `can_add_materials` | D10 |
| GET /admin/documents/{id} | — | 200 DocumentResponse | 401/403/404; оператор или `can_add_materials` | D10 |
| POST /admin/documents/{id}/pages/{pageNo}/review | PageReviewRequest | 202 ReviewAcceptedResponse | 400/401/403/404/409 | D10 |
| POST /admin/jobs/{id}/retry | без тела | 202 AcceptedResponse | 400/401/403/404/409 | D10 |
| POST /admin/news | ManualNewsRequest | 202 AcceptedResponse | 400/401/403; оператор или `can_add_materials` | D17 |

Материалы (25.10, страница «Добавить материал» макета 03): `MeResponse.can_add_materials` — true у операторов и учётки `demo`; аналитика банка общая для всех компаний, поэтому учёткам отдельных компаний загрузка закрыта (403). Такие пользователи видят и страницы загруженных PDF (`GET /research/{id}/pages/{n}`) без ссылки из своей карточки. Проверка страниц PDF (`.../review`) и повтор заданий — только оператор. В `DocumentSummaryDto` `uploaded_at` — время добавления документа, `uploaded_by_me` — документ загружен текущим пользователем (фильтр «Ваши загрузки»). `llm_key_required` в `DocumentSummaryDto` и `DocumentResponse` — документ failed, и все его страницы не распознаны только потому, что не задан ключ модели («Ждёт ключа модели»); у такой страницы `error` объясняет, что после настройки ключа нужен повтор задания. Это отдельный флаг, `ocr_status` прежний (`failed`), чтобы не менять перечисление статусов на фронте.

`200` означает готовый результат. `202` означает принятую работу, не готовый анализ. `GET` не ставит новые LLM-задания; если карточки нет, возвращается 404 `insight_not_prepared`, сайт может вызвать POST один раз по действию пользователя. При failed POST ставит повтор (202), всего не больше трёх попыток на карточку (первая и два повтора), дальше 409 `analysis_failed`; `feed.items[].retry_available` говорит, показывать ли кнопку «Повторить». GET для failed по-прежнему отвечает 409 `analysis_failed`. Нет бесконечного автоматического перезапуска из GET/рендера.

В AcceptedResponse status_url=`/api/jobs/{job_id}`. После done JobResponse.result_type: `feed / insight / chat / document / document_review`, result_id — соответствующий ID. Для chat result_id — chat_message.id, сайт обновляет GET chat. Для feed — company_id, сайт обновляет GET feed. По job ответа модели и секретов не выдаём. Для общих сборов создаётся безопасная пользовательская job-обёртка либо отдельная refresh-job, без передачи доступа к чужой job.

Feed.role — только выбор готовности/ID нужного варианта карточки; порядок/score общий. При отсутствии snapshot 200 с items=[], last_updated_at=null и refresh_status=idle, кнопка refresh доступна. Источник со сбоем не стирает последний валидный snapshot. Период сервера виден даже в пустом состоянии. refresh_status: idle/queued/processing/failed; завершённый сбор → idle. Done refresh-job не обещает, что весь полный анализ уже готов: смотрим analysis_status каждого элемента.

Поля строки ленты (ALF-59), все считает код, модель их не пишет: `relevance.significance` — low/medium/high по base (ниже 0,40 — одна связь с компанией, 0,40–0,50 — две, от 0,50 — три; есть и в InsightDto); `relevance.reasons[].label` — короткая метка до 40 знаков (тема, продукт, страна), полный текст остаётся в `text`; `insight_headline` — заголовок готовой карточки выбранной роли, `headline` — заголовок новости; `direction` — risk/opportunity/neutral по последствиям готовой карточки (знак первого из рисков и возможностей; без них neutral), null без карточки; `queued_at` — когда карточка поставлена в очередь, null у готовой и у непоставленной; `retry_available` — у failed, пока не исчерпаны попытки. `feed.last_collected_at` — последний успешный сбор любого источника; `feed.collection_failed` — у всех включённых источников последняя проверка закончилась ошибкой (это не refresh_status: тот про пересчёт ленты). Поля карточки для макета 04, все считает код при выдаче, в том числе у карточек, сохранённых раньше: `direction` — то же правило, что в строке ленты; `news_period_from`/`news_period_to` — окно новостей карточки (семь суток по Москве до `as_of`, как в ленте); `company_traits` — полоска «Почему это касается вас»: `code`, `label` и `amount_rub` (только у группы) — отрасль, продукты, страны и контрагенты из причин отбора в их порядке и группа с известной выручкой участников («Группа · 26,0 млрд ₽», без выручки — «Группа компаний»), факторы события сюда не входят; `bank_data_as_of` — дата данных банка о клиенте (сборка профиля компании), не дата обзора банка. Поля заполняются в `GET`/`POST .../insight`; в других ответах `company_traits` и `bank_data_as_of` могут быть null. Шаги карточки: `GET /insights/{id}/steps` — название шага, итог, статус done/skipped/failed и длительность, без аргументов, результатов и промптов.

Опрос feed: при открытии, каждые 120 секунд и по кнопке. Опрос конкретной job: каждые 3 секунды до done/failed; при уходе со страницы выключить. Повторный POST для уже активной карточки возвращает тот же job_id. Нет SSE/WebSocket/пушей; внутренние шаги — только сводка /steps. Все ошибки — ApiError, без traceback и промптов. Полезные коды: unauthorized, data_not_ready (вход до импорта компаний), forbidden_company, forbidden_role, not_found, insight_not_prepared, analysis_failed, invalid_input, question_limit, rate_limited, budget_exhausted, source_unavailable, processing_failed, stale_review.

Чат доступен только у ready insight. question: 1–1000 символов после trim; до 10 принятых вопросов на user × insight, включая queued и отказы. Неудачный технический повтор той же job не расходует ещё один вопрос. Сайт блокирует повторную отправку, пока вопрос в обработке. Невалидный JSON/ошибка бюджета не показывается как успешный ответ. Status ChatAnswerDto: answer/refusal; у refusal claims=[] и refusal_reason, у answer refusal_reason=null. История ограничена текущей карточкой и пользователем.

Feedback.kind: like/dislike/hide; reason: not_relevant/wrong_fact/obvious/null. Hide относится к событию компании, сохраняется между CEO/CFO данного пользователя; общий rank не пересчитывается. Новые пользовательские отзывы не меняют веса.

PDF: стартовый лимит 50 MiB и 50 страниц на файл, MIME + сигнатура `%PDF-`, случайное серверное имя, путь пользователя не используется. В текущем пакете самый большой файл FI_Strategy — 34 890 230 байт, поэтому предел 25 MiB не подходит; лимиты API и reverse proxy должны совпасть. Повтор sha256 возвращает существующий документ и действующую/завершённую job без нового OCR. Если OCR ранее failed — повтор только явным retry. Для исходного пакета документ регистрируется тем же сервисом из CLI.

Review: только существующие chunk_id/chart local_id данной страницы. CorrectedTables/CorrectedChart сохраняются вместе с review. Нельзя поставить reviewed странице, оставив непроверенный график/блок; сервер вычисляет aggregate review_status. Конкурирующая правка не перетирает новый OCR: версия документа передаётся в заголовке If-Match; иначе 409 stale_review. По новой версии — переиндексация и инвалидирование зависимых результатов, не синхронный повтор всех LLM в HTTP.

Добавление/редактирование источников, журнал расходов и выбор режима через операторскую панель — желательно, не часть этих обязательных endpoints. Редактирования .env в браузере нет.

## Журнал действий пользователя

`POST /api/insights/{id}/activity` принимает только `event_id` (UUID), `type`, `evidence_id`, `action_id`. Все четыре поля обязательны; неиспользуемые ссылки равны null. Разрешённые браузерные типы: insight_opened, reasons_opened, source_opened, action_interacted, manager_contact_requested (кнопка «Связаться с менеджером»: только запись действия, заявка никуда не отправляется). Компания определяется по карточке, пользователь — по сессии, время — сервером. Для source_opened ссылка должна существовать в evidence; для action_interacted — в actions. Нет действия в интерфейсе — нет события.

Точный повтор event_id и содержимого возвращает 204 без повторного подсчёта. Другой payload с тем же ID — 409 event_conflict. Чужая карточка — 403; неизвестная — 404; неготовая — 409 insight_not_prepared. Неизвестные поля, произвольный текст, поддельный user/company/time, недействительные ссылки и браузерный chat_question_accepted отклоняются. Последний тип пишет только серверный UserActivityService после принятия вопроса; D09/A13 подключают его в транзакцию сохранения chat_message. Endpoint не подтверждает фактическое открытие внешнего сайта, а фиксирует заявленный UI переход.

Имя cookie для D04 — alfa_session. SessionService.HashToken вычисляет SHA-256 случайного токена; оригинал остаётся только в HttpOnly cookie. Каждый запрос проверяет срок, отзыв сессии и активность пользователя. Изменяющие запросы требуют Origin из Auth:AllowedOrigins; при отсутствии настройки разрешён только origin текущего запроса. Для HTTPS за reverse proxy нужно явно настроить разрешённый внешний origin. Cookie Secure/SameSite=Lax выставляет D04. Не включать CORS с произвольным origin и credentials.

## Желательно: ограниченные дополнения

| Метод / путь | Формат | Задача |
| --- | --- | --- |
| GET /admin/usage?date=YYYY-MM-DD | OperatorUsageResponse, сутки Europe/Moscow; если date нет — текущие | D15 |
| GET /admin/config | ModelConfigResponse, только выбранные модели/режимы/лимиты, без ключа и .env | D15 |
| GET /admin/sources | OperatorSourcesResponse, только список/последний статус | D15 |

Все endpoints требуют оператора. В D15 не входит runtime-редактор источников: в хакатоне добавление проходит через проверенный sources.json и обычный reload/restart. Редактор источников/документов не означает редактирование серверных секретов.

ManualNewsRequest (D17, сделано 25.10): обязателен `url` статьи с сайта включённого источника, `source_id` необязателен — без него источник подбирается по хосту ссылки. `title`, `text`, `published_at` не используются: ручной текст без ссылки не принимается, у новости должен быть первоисточник. Ссылка не с сайта источника — 400 со списком допустимых сайтов, allowlist не обходится. Статью загружает `CollectManualNews` в Worker: сохраняет `news_raw` с важностью 1 (без фильтра заголовков) и ставит обычный разбор, карточку вручную не создаёт; дата — из статьи, иначе время загрузки. JobResponse: done со stage `collected` или `duplicate` (статья уже была в базе; отсеянная фильтром заголовков поднимается до важности 1 и считается `collected`); failed с ошибкой `news_unavailable` (страница не загрузилась), `news_empty` (нет текста статьи) или `news_outside_window` (старше семи дней). Новость появится в ленте после разбора и следующего обновления ленты, если пройдёт отбор для компании.
# HTTP-контракт MVP

DTO: [Api.cs](Api.cs), [api.ts](api.ts); общие поля карточки в [Domain.cs](Domain.cs). Пути ниже нормативные для задач Дани. Здесь пока нет работающих endpoints.

Все пути начинаются `/api`. Авторизация — cookie HttpOnly; production cookie Secure/SameSite=Lax, same-origin reverse proxy сайта/API. Изменяющие запросы проверяют Origin/CSRF; API не принимает произвольный cross-origin credentialed вызов. Один общий middleware/сервис доступа пишет Артём/проверяет в A02, Даня применяет его во всех своих endpoints. Пользователь имеет доступ только к CLIENT-компаниям из MeResponse, операторские действия требуют IsOperator. Секреты и raw ORM entities наружу не отдаём.

| Метод / путь | Вход | Успешный ответ | Ошибки / права | Владелец |
| --- | --- | --- | --- | --- |
| POST /auth/login | LoginRequest | 200 MeResponse + cookie | 400/401/429 | D04 |
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
| GET /jobs/{id} | — | 200 JobResponse | 401/403/404; только владелец либо оператор | D09 |
| POST /admin/documents | multipart: file PDF; title string; published_on YYYY-MM-DD или пусто; source_url URL или пусто | 202 DocumentAcceptedResponse | 400/401/403/413/415 | D10 |
| GET /admin/documents | — | 200 DocumentListResponse, включая стартовые 9 документов | 401/403 | D10 |
| GET /admin/documents/{id} | — | 200 DocumentResponse | 401/403/404 | D10 |
| POST /admin/documents/{id}/pages/{pageNo}/review | PageReviewRequest | 202 ReviewAcceptedResponse | 400/401/403/404/409 | D10 |
| POST /admin/jobs/{id}/retry | без тела | 202 AcceptedResponse | 400/401/403/404/409 | D10 |

`200` означает готовый результат. `202` означает принятую работу, не готовый анализ. `GET` не ставит новые LLM-задания; если карточки нет, возвращается 404 `insight_not_prepared`, сайт может вызвать POST один раз по действию пользователя. При failed POST возвращает 409 `analysis_failed`, повтор запускает оператор через retry. Нет бесконечного автоматического перезапуска из GET/рендера.

В AcceptedResponse status_url=`/api/jobs/{job_id}`. После done JobResponse.result_type: `feed / insight / chat / document / document_review`, result_id — соответствующий ID. Для chat result_id — chat_message.id, сайт обновляет GET chat. Для feed — company_id, сайт обновляет GET feed. По job ответа модели и секретов не выдаём. Для общих сборов создаётся безопасная пользовательская job-обёртка либо отдельная refresh-job, без передачи доступа к чужой job.

Feed.role — только выбор готовности/ID нужного варианта карточки; порядок/score общий. При отсутствии snapshot 200 с items=[], last_updated_at=null и refresh_status=idle, кнопка refresh доступна. Источник со сбоем не стирает последний валидный snapshot. Период сервера виден даже в пустом состоянии. refresh_status: idle/queued/processing/failed; завершённый сбор → idle. Done refresh-job не обещает, что весь полный анализ уже готов: смотрим analysis_status каждого элемента.

Опрос feed: при открытии, каждые 120 секунд и по кнопке. Опрос конкретной job: каждые 3 секунды до done/failed; при уходе со страницы выключить. Повторный POST для уже активной карточки возвращает тот же job_id. Нет SSE/WebSocket/пушей/показа внутренних шагов. Все ошибки — ApiError, без traceback и промптов. Полезные коды: unauthorized, forbidden_company, forbidden_role, not_found, insight_not_prepared, analysis_failed, invalid_input, question_limit, rate_limited, budget_exhausted, source_unavailable, processing_failed, stale_review.

Чат доступен только у ready insight. question: 1–1000 символов после trim; до 10 принятых вопросов на user × insight, включая queued и отказы. Неудачный технический повтор той же job не расходует ещё один вопрос. Сайт блокирует повторную отправку, пока вопрос в обработке. Невалидный JSON/ошибка бюджета не показывается как успешный ответ. Status ChatAnswerDto: answer/refusal; у refusal claims=[] и refusal_reason, у answer refusal_reason=null. История ограничена текущей карточкой и пользователем.

Feedback.kind: like/dislike/hide; reason: not_relevant/wrong_fact/obvious/null. Hide относится к событию компании, сохраняется между CEO/CFO данного пользователя; общий rank не пересчитывается. Новые пользовательские отзывы не меняют веса.

PDF: стартовый лимит 50 MiB и 50 страниц на файл, MIME + сигнатура `%PDF-`, случайное серверное имя, путь пользователя не используется. В текущем пакете самый большой файл FI_Strategy — 34 890 230 байт, поэтому предел 25 MiB не подходит; лимиты API и reverse proxy должны совпасть. Повтор sha256 возвращает существующий документ и действующую/завершённую job без нового OCR. Если OCR ранее failed — повтор только явным retry. Для исходного пакета документ регистрируется тем же сервисом из CLI.

Review: только существующие chunk_id/chart local_id данной страницы. CorrectedTables/CorrectedChart сохраняются вместе с review. Нельзя поставить reviewed странице, оставив непроверенный график/блок; сервер вычисляет aggregate review_status. Конкурирующая правка не перетирает новый OCR: версия документа передаётся в заголовке If-Match; иначе 409 stale_review. По новой версии — переиндексация и инвалидирование зависимых результатов, не синхронный повтор всех LLM в HTTP.

Добавление/редактирование источников, журнал расходов и выбор режима через операторскую панель — желательно, не часть этих обязательных endpoints. Редактирования .env в браузере нет.

## Желательно: ограниченные дополнения

| Метод / путь | Формат | Задача |
| --- | --- | --- |
| GET /admin/usage?date=YYYY-MM-DD | OperatorUsageResponse, сутки Europe/Moscow; если date нет — текущие | D15 |
| GET /admin/config | ModelConfigResponse, только выбранные модели/режимы/лимиты, без ключа и .env | D15 |
| GET /admin/sources | OperatorSourcesResponse, только список/последний статус | D15 |
| POST /admin/news | ManualNewsRequest → 202 AcceptedResponse | D17 |

Все endpoints требуют оператора. В D15 не входит runtime-редактор источников: в хакатоне добавление проходит через проверенный sources.json и обычный reload/restart. Редактор источников/документов не означает редактирование серверных секретов.

ManualNewsRequest: source_id существующего источника; либо url (title/text/published_at=null, загружаем разрешённую статью), либо text+title+published_at с url=null. Текст до 100 000 символов, заголовок до 300; дата обязательна для ручного текста, окно то же. CollectManualNewsJob сохраняет CollectedNews/news_raw и вызывает обычный ParseNews, не создаёт insight вручную. Недоступный/неразрешённый URL — 400, а не обход allowlist.
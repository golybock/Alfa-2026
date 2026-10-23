# Входные данные, конфигурация и ручные результаты

Пути в этом файле — от корня репозитория. Исходные CSV/PDF не переименовываем и не переписываем. Заготовки не означают, что сбор, OCR и сверка уже выполнены.

## CSV организаторов

UTF-8, разделитель `;`, десятичная точка, флаги `0.0 / 1.0`. ID и ОКВЭД — строки.

| Файл | Реальные поля | Преобразование |
| --- | --- | --- |
| data/df_Uid_data_hack.csv | SYNTHETIC_U; ORG_TYPE; OKFS_NAME; COMPANY_AGE_YEARS_GRADATION; ECO_MARK_LABEL; MAINOKVED_CODE; MAINOKVED_NAME; NEGATIVEMARK; AMOUNT_WORKERS_FNS_GRADATION; AMOUNT_WORKERS_SPARK_GRADATION; CL_TYPE; CL_PRODUCT_RKO_MARK; CL_PRODUCT_GAR_MARK; CL_PRODUCT_LOA_MARK; CL_PRODUCT_LEA_MARK; CL_PRODUCT_DEP_MARK; CL_PRODUCT_ZPP_MARK; FTS_FLAG; FTS_COUNTRY_NAME; FACTORING_MARK; REVENUE_COMPANY; GROUP_ID; CITY; REGION | company.raw_data сохраняет все 24 исходных поля; чистые поля ниже |
| data/df_Uid_payment_hack.csv | BENEFICIARY; PAYER; PAY_AMT; PAY_CNT | payment.beneficiary/payer/pay_amt/pay_cnt; самопереводы исключаются |
| data/df_Fid_Uid_link_hack.csv | Не читаем в MVP | Физлица и аффилированность отложены |

Mapping CompanyProfile: SYNTHETIC_U→CompanyId; GROUP_ID→GroupId; MAINOKVED_CODE→OkvedCode и первые 2 цифры→Okved2; MAINOKVED_NAME→IndustryName; REGION/CITY; COMPANY_AGE_YEARS_GRADATION→AgeBand; ECO_MARK_LABEL→Lifecycle; REVENUE_COMPANY→Revenue. NEGATIVEMARK — ключевой собственник, не риск. Число работников — raw_data, не обязательный фактор score.

Продукты при флаге 1: CL_PRODUCT_RKO_MARK→rko, GAR→guarantee, LOA→loan, LEA→leasing, DEP→deposit, ZPP→payroll, FACTORING_MARK→factoring. FTS_FLAG не банковский продукт; страны из FTS_COUNTRY_NAME разделяем запятыми, trim, нормализуем по согласованному словарю. В исходнике встречаются `КИТАЙ, ГОНКОНГ` и `КИТАЙ, ЛИТВА, ГРУЗИЯ`; неизвестные наименования не превращаем в выдуманный ISO-код. Новость и профиль используют один словарь русских названий стран. Пустые поля→null, пустые списки→[].

При импорте нужен CLIENT, выручка 1000→null, конфликтующие варианты одного ID→отчёт и исключение, одинаковые строки→одна. Платёжные профили соединяются LEFT JOIN; участник без company не исчезает из знаменателя. Отрицательные/нечитаемые суммы не превращаем в абсолютные значения: помещаем в отчёт качества и исключаем из расчёта неотрицательных долей до разбирательства. Причины и количество видны в import_report.json.

## Конфигурация источников

Готового выбранного набора нет: K01 создаёт `data/source_checks.csv` и `data/sources.json`, находит точные RSS/Atom и фиксирует кандидатов, результаты проверки и выбор. `contracts/sources.seed.json`, если остался в старом комплекте, не является входом или утверждённой конфигурацией. Начальные кандидаты — официальные RSS Банка России, деловые рубрики РБК, Коммерсанта и других регуляторов/изданий; точные адреса Кирилл находит на сайтах сам.

CSV: UTF-8, разделитель запятая, экранирование по CSV; JSON — массив объектов. Одна запись на ленту, общий стабильный id в обоих файлах. Список полей CSV:

```text
id,name,feed_url,kind,rubric,publisher,discovered_at,checked_at,http_status,xml_ok,items_in_window,samples_checked,full_text_ok,published_at_ok,selected,selection_reason,error,sample_urls
```

`kind`: official/media; даты ISO 8601 с часовым поясом. `selected`, `xml_ok`, `full_text_ok`, `published_at_ok` — true/false; неизвестный результат проверки — пустое поле. `items_in_window` — фактически посчитанное число записей feed за недельное окно, не оценка всего сайта; `sample_urls` — JSON-массив внутри CSV-ячейки. `http_status`, если не виден, пустой. Издатель (`publisher`) позволяет не считать три RSS ЦБ тремя независимыми организациями. Записываем и неработающие/отклонённые источники.

JSON: `id,name,feed_url,kind,rubric,publisher,allowed_hosts,enabled,poll_seconds,reliability,validation_status,checked_at,selection_reason`. `allowed_hosts` — массив точных доменов feed и статей; список сверяется с Даней. `poll_seconds=120`; reliability=1 для official, .9 для проверенного media — заданные правила, Кирилл новые веса не придумывает. `validation_status`: pending/checked/rejected. `enabled=true` только для выбранного после проверки/согласования источника; в CSV у него selected=true. Неизвестные/недоступные записи выключены; причина сохранена.

Для каждой ленты открываем до трёх доступных статей в текущем недельном окне и сверяем заголовок/дату/полный текст без меню и paywall. Если статей меньше — записываем фактическое число. После готовности D08 проверяем также автоматическое извлечение с Даней. Нехватка покрытия двух отраслей — повод найти дополнительные независимые деловые источники; фиксированное число RSS или принудительное количество новостей не является критерием готовности.

Публичная статья: first-party URL из feed, тот же host или host из allowed_hosts. После редиректа повторная проверка host и запрет private/loopback адресов; не делаем произвольную серверную загрузку URL из чата. HTTP timeout 15 секунд, HTML/XML до 5 MiB, по домену не чаще 1 запроса/секунду. Ошибка — запись last_error и следующий источник. Сначала feed published/updated, затем явный article datePublished/time; не заменять датой загрузки. Неизвестная дата остаётся null и не попадает в ленту. Date-only опубликованной даты — 00:00 в часовом поясе источника, пометка точности в журнале, не дата LLM.

Сборщик D08 возвращает CollectedNews: source_id/canonical_url/title/text/published_at/fetched_at/text_sha256. Пробелы текста нормализуются перед SHA-256; исходный текст, по которому проверяются цитаты, фиксируется в news_raw. Ключ canonical URL очищен от tracking-параметров/фрагмента, но значимые параметры статьи сохраняются. Затем A07 делает NewsExtraction; D08 не определяет score и не вызывает LLM.

## Индекс девяти PDF

Файл [data/bank_research/index.json](../data/bank_research/index.json) подготовлен по реальным файлам: путь, SHA-256, страницы и наличие текстового слоя. Название, дата и исходный URL, пока не сверены Кириллом, остаются null; это не review OCR. Вход IndexPdf: DocumentId, файл и метаданные из индекса/загрузки.

K02 заполняет title/published_on/source_url по титульной странице/данным задания. Если дата неизвестна — null и примечание в data/bank_research/index-review.md, не дата создания файла. Российская сталь — материал 2021 года, не 2026. Папку не переносим и повторно скачивать существующие файлы не требуется.

При регистрации документа незаполненный title заменяется именем файла без расширения, дата остаётся null; исходная запись индекса сохраняет неизвестное значение, пока Кирилл не сверил метаданные.

## OCR и сверка

Модель GLM получает одну PNG-страницу и возвращает PdfPageExtraction. Номер страницы/document_id/model/version/review назначает код. Block.kind: fact/bank_view/forecast/disclaimer. TableDraft.rows — строковые значения с исходными единицами/прочерками, без угадывания пустых ячеек.

ChartDraft.region=[x,y,width,height], координаты нормированы в [0,1] относительно всей страницы. ReadingMethod: label/axis_estimate; у label approximate=false, у axis_estimate approximate=true. Для точки либо value, либо low/high, либо ни одного числа. Пример только формата: series="пример", period="2025", low=8, high=9, unit="%", approximate=true, reading_method="axis_estimate" → «по графику, приблизительно 8–9%». Это не число из выданного PDF. Нечитаемый график даёт description, points=[].

ReviewableChartDto добавляет серверный review_status; модель не может объявить сама себя проверенной. Даже точно подписанные числа графика допускаются в карточку как численные входы после ручной сверки. Текст до сверки доступен с меткой. K03 пишет data/bank_research/ocr-review.csv: document_id,page_no,chunk_id,chart_local_id,issue,correction,reviewed_at; факт фикса ошибки подтверждается после применения через D10 и повторного просмотра.

## Правила и ручная разметка

K04 не сочиняет веса самостоятельно: получает от A04 кандидатов двух отраслей и черновик Артёма из A01/A04, проверяет обоснование и заносит замечания. Утверждает Артём.

```text
data/taxonomy.json: {version,reviewed_by,reviewed_at,
  factors: [{code,name,description,keywords}],
  product_rules: [{product,factor,channel}],
  sector_sensitivity: [{okved2,factor,weight,sign,channel}]}
data/eval/relevance.csv: company_id,event_id,label,channel,comment,reviewed_by
data/eval/chat.csv: question,expected,reason
data/eval/card-review.csv: insight_id,claim_id,refs_ok,numbers_ok,company_link_ok,decision_specific,comment
data/eval/source-coverage.csv: source_id,from,to,fetched,dated_in_window,parsed,eligible_company_a,eligible_company_b,error
```

label relevance: 0 нет подтверждённой связи, 1 слабая косвенная, 2 полезно компании, 3 один из приоритетных сигналов. Это ручная метка для eval, не отдельная шкала влияния в UI. Размечаем 20 событий × 2 компании без просмотра текущего rank; все 40 строк сохраняются, включая нулевые. Если 20 новых событий пока нет — фиксируем фактическое количество и причину, не добавляем старые или мусорные новости ради числа. Артём просматривает разногласия/выборку.

Chat eval: 10 нормальных вопросов и 10 явно посторонних/injection/чужая компания; expected=answer/refusal. Для неоднозначного вопроса Кирилл ставит вопрос Артёму, не решает политику доступа. Карточки: сначала все полные демо-карточки, затем новые готовые; ошибки считаем по claims, а не по числу красивых экранов.

## Инструменты агента и задания

Разрешены `get_company_profile`, `get_event`, `get_group_summary`, `get_payment_exposure`, `search_bank_research`. Первые три без company_id/event_id в model arguments: ID берутся из AgentContext, который создал код после проверки доступа. Платежи: GetPaymentArguments, направления раздельно; выдаём агрегаты и покрытие, не предполагаемые поставки. Поиск: SearchResearchArguments, limit от 1 до 6, query до 500 символов. Модель не задаёт SQL, файловый путь и произвольный URL. Каждый ToolResult регистрирует E1/E2… в одном реестре без переиспользования ID под другим содержимым.

Строка background_job.kind совпадает с названием record без суффикса Job: ImportCsv, RebuildProfiles, CollectNews, ParseNews, RefreshFeed, IndexPdf, OcrPdfPage, FinalizePdf, EmbedContent, GenerateInsight, AnswerChat, PrecomputeDemo. Payload — соответствующий record из backend/AlfaNews.Contracts/Processing. Желательный CollectManualNews использует CollectManualNewsJob и не нужен для автоматического пути MVP. Parent/child не обязаны образовывать универсальный workflow engine. Job owner/company назначаются API/кодом, не из payload пользователя.

LlmTask: news_extract/plan/draft/repair/chat/scope_gate → DeepSeek v4.1 Flash; pdf_ocr/critic → GLM 5.3 Flash high. До 32000 токенов выхода, chat/scope_gate до 8000; ключ/model ID/параметр reasoning из серверной конфигурации и проверки A06, не выдуманный ID провайдера. ScopeGateResult.label=in_scope/out_of_scope/injection; CriticResult.issues — ссылки только на существующие ClaimId. InsightDraft не содержит company_id/score/evidence/verification: эти поля присоединяет код.
-- Целевая схема v1 для A02; не применена к какой-либо базе.
-- UUID создаёт приложение. Модель embedding: multilingual-e5-base, 768 значений.
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE company (
    id text PRIMARY KEY,
    cl_type text NOT NULL CHECK (cl_type = 'CLIENT'),
    group_id text,
    main_okved_code text NOT NULL,
    main_okved_name text,
    region text,
    city text,
    revenue numeric(24,2),
    raw_data jsonb NOT NULL,
    dataset_version text NOT NULL,
    quality_flags jsonb NOT NULL DEFAULT '[]',
    imported_at timestamptz NOT NULL
);
CREATE INDEX company_group_idx ON company(group_id);

CREATE TABLE payment (
    payer text NOT NULL,
    beneficiary text NOT NULL,
    pay_amt numeric(28,2) NOT NULL,
    pay_cnt bigint NOT NULL CHECK (pay_cnt >= 0),
    dataset_version text NOT NULL,
    PRIMARY KEY (payer, beneficiary),
    CHECK (payer <> beneficiary)
);
CREATE INDEX payment_beneficiary_idx ON payment(beneficiary);

CREATE TABLE company_profile (
    company_id text PRIMARY KEY REFERENCES company(id),
    data_version text NOT NULL,
    profile_json jsonb NOT NULL,
    updated_at timestamptz NOT NULL
);
CREATE TABLE group_profile (
    group_id text PRIMARY KEY,
    data_version text NOT NULL,
    profile_json jsonb NOT NULL,
    updated_at timestamptz NOT NULL
);
CREATE TABLE factor (
    code text PRIMARY KEY,
    name text NOT NULL,
    description text NOT NULL,
    keywords jsonb NOT NULL DEFAULT '[]',
    version text NOT NULL
);
CREATE TABLE sector_sensitivity (
    okved2 text NOT NULL,
    factor_code text NOT NULL REFERENCES factor(code),
    weight numeric(6,5) NOT NULL CHECK (weight BETWEEN 0 AND 1),
    sign text NOT NULL CHECK (sign IN ('positive','negative','mixed')),
    channel text NOT NULL,
    reviewed_by text NOT NULL,
    reviewed_at timestamptz NOT NULL,
    version text NOT NULL,
    PRIMARY KEY (okved2, factor_code)
);
CREATE TABLE product_factor_rule (
    product_code text NOT NULL,
    factor_code text NOT NULL REFERENCES factor(code),
    channel text NOT NULL,
    version text NOT NULL,
    PRIMARY KEY (product_code, factor_code)
);

CREATE TABLE app_user (
    id uuid PRIMARY KEY,
    login text UNIQUE NOT NULL,
    password_hash text NOT NULL,
    display_name text NOT NULL,
    is_operator boolean NOT NULL DEFAULT false,
    active boolean NOT NULL DEFAULT true
);
CREATE TABLE auth_session (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES app_user(id),
    token_hash text UNIQUE NOT NULL,
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    revoked_at timestamptz
);
CREATE TABLE user_company_access (
    user_id uuid NOT NULL REFERENCES app_user(id),
    company_id text NOT NULL REFERENCES company(id),
    role text NOT NULL CHECK (role IN ('ceo','cfo')),
    PRIMARY KEY (user_id, company_id, role)
);

CREATE TABLE source (
    id text PRIMARY KEY,
    name text NOT NULL,
    feed_url text NOT NULL,
    kind text NOT NULL CHECK (kind IN ('official','media')),
    allowed_hosts jsonb NOT NULL,
    enabled boolean NOT NULL DEFAULT false,
    poll_seconds integer NOT NULL DEFAULT 3600 CHECK (poll_seconds >= 60),
    reliability numeric(3,2) NOT NULL CHECK (reliability BETWEEN 0 AND 1),
    validation_status text NOT NULL,
    last_checked_at timestamptz,
    last_success_at timestamptz,
    last_error text,
    is_specialized boolean NOT NULL DEFAULT false
);
CREATE TABLE news_raw (
    id uuid PRIMARY KEY,
    source_id text NOT NULL REFERENCES source(id),
    canonical_url text UNIQUE NOT NULL,
    title text NOT NULL,
    text text NOT NULL,
    text_sha256 text NOT NULL,
    published_at timestamptz,
    fetched_at timestamptz NOT NULL,
    status text NOT NULL CHECK (status IN ('queued','processing','accepted','duplicate','rejected','failed')),
    rejection_reason text,
    last_error text,
    embedding vector(768),
    filter_json jsonb,
    importance integer CHECK (importance IS NULL OR importance BETWEEN 1 AND 3)
);
CREATE INDEX news_pending_idx ON news_raw(status, importance, fetched_at);
CREATE INDEX news_hash_idx ON news_raw(text_sha256);
CREATE INDEX news_date_idx ON news_raw(published_at DESC);
CREATE TABLE event (
    id uuid PRIMARY KEY,
    news_raw_id uuid UNIQUE NOT NULL REFERENCES news_raw(id),
    content_sha256 text UNIQUE NOT NULL,
    headline text NOT NULL,
    event_date date,
    published_at timestamptz NOT NULL,
    extraction_json jsonb NOT NULL,
    input_version text NOT NULL,
    search_text tsvector NOT NULL,
    embedding vector(768),
    created_at timestamptz NOT NULL
);
CREATE INDEX event_date_idx ON event(published_at DESC);
CREATE INDEX event_fts_idx ON event USING gin(search_text);
CREATE TABLE event_source (
    event_id uuid NOT NULL REFERENCES event(id),
    news_raw_id uuid NOT NULL REFERENCES news_raw(id),
    PRIMARY KEY (event_id, news_raw_id)
);
CREATE TABLE event_tag (
    event_id uuid NOT NULL REFERENCES event(id),
    kind text NOT NULL CHECK (kind IN ('factor','okved2','country','region','commodity')),
    code text NOT NULL,
    PRIMARY KEY (event_id, kind, code)
);
CREATE INDEX event_tag_lookup_idx ON event_tag(kind, code);

CREATE TABLE research_doc (
    id uuid PRIMARY KEY,
    sha256 text UNIQUE NOT NULL,
    file_path text NOT NULL,
    title text NOT NULL,
    source_url text,
    published_on date,
    page_count integer NOT NULL CHECK (page_count > 0),
    ocr_status text NOT NULL CHECK (ocr_status IN ('queued','processing','ready','partial','failed')),
    review_status text NOT NULL CHECK (review_status IN ('not_reviewed','partially_reviewed','reviewed')),
    version text NOT NULL,
    uploaded_by uuid REFERENCES app_user(id),
    created_at timestamptz NOT NULL
);
CREATE TABLE research_page (
    id uuid PRIMARY KEY,
    document_id uuid NOT NULL REFERENCES research_doc(id),
    page_no integer NOT NULL CHECK (page_no > 0),
    image_path text NOT NULL,
    ocr_status text NOT NULL CHECK (ocr_status IN ('queued','processing','ready','failed')),
    ocr_json jsonb,
    review_status text NOT NULL CHECK (review_status IN ('not_reviewed','partially_reviewed','reviewed')),
    reviewed_by uuid REFERENCES app_user(id),
    reviewed_at timestamptz,
    last_error text,
    UNIQUE (document_id, page_no)
);
CREATE TABLE research_chunk (
    id uuid PRIMARY KEY,
    page_id uuid NOT NULL REFERENCES research_page(id),
    local_id text NOT NULL,
    kind text NOT NULL CHECK (kind IN ('fact','bank_view','forecast','disclaimer')),
    text text NOT NULL,
    tables_json jsonb NOT NULL DEFAULT '[]',
    chart_data jsonb NOT NULL DEFAULT '[]',
    tags_json jsonb NOT NULL,
    review_status text NOT NULL CHECK (review_status IN ('not_reviewed','partially_reviewed','reviewed')),
    review_note text,
    version text NOT NULL,
    search_text tsvector NOT NULL,
    embedding vector(768),
    UNIQUE (page_id, local_id)
);
CREATE INDEX research_fts_idx ON research_chunk USING gin(search_text);

CREATE TABLE feed_snapshot (
    company_id text PRIMARY KEY REFERENCES company(id),
    as_of timestamptz NOT NULL,
    news_period_from timestamptz NOT NULL,
    news_period_to timestamptz NOT NULL,
    input_version text NOT NULL,
    items_json jsonb NOT NULL,
    last_updated_at timestamptz NOT NULL,
    collection_started_at timestamptz,
    refresh_status text NOT NULL
);
CREATE TABLE insight (
    id uuid PRIMARY KEY,
    company_id text NOT NULL REFERENCES company(id),
    event_id uuid NOT NULL REFERENCES event(id),
    role text NOT NULL CHECK (role IN ('ceo','cfo')),
    input_version text NOT NULL,
    status text NOT NULL CHECK (status IN ('queued','processing','ready','failed')),
    body_json jsonb,
    last_error text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    UNIQUE (company_id, event_id, role, input_version)
);
CREATE TABLE chat_message (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES app_user(id),
    insight_id uuid NOT NULL REFERENCES insight(id),
    question text NOT NULL,
    status text NOT NULL CHECK (status IN ('queued','processing','ready','failed')),
    answer_json jsonb,
    created_at timestamptz NOT NULL
);
CREATE INDEX chat_history_idx ON chat_message(user_id, insight_id, created_at);
CREATE TABLE feedback (
    user_id uuid NOT NULL REFERENCES app_user(id),
    company_id text NOT NULL REFERENCES company(id),
    event_id uuid NOT NULL REFERENCES event(id),
    kind text NOT NULL CHECK (kind IN ('like','dislike','hide')),
    reason text,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (user_id, company_id, event_id)
);

CREATE TABLE background_job (
    id uuid PRIMARY KEY,
    kind text NOT NULL,
    payload jsonb NOT NULL,
    status text NOT NULL CHECK (status IN ('queued','running','done','failed')),
    priority integer NOT NULL,
    owner_user_id uuid REFERENCES app_user(id),
    company_id text REFERENCES company(id),
    created_at timestamptz NOT NULL,
    started_at timestamptz,
    finished_at timestamptz,
    attempts integer NOT NULL DEFAULT 0,
    stage text NOT NULL,
    result_type text,
    result_id text,
    last_error text
);
CREATE INDEX job_queue_idx ON background_job(status, priority, created_at);
CREATE TABLE agent_run (
    id uuid PRIMARY KEY,
    job_id uuid NOT NULL REFERENCES background_job(id),
    insight_id uuid REFERENCES insight(id),
    input_version text NOT NULL,
    status text NOT NULL,
    started_at timestamptz NOT NULL,
    finished_at timestamptz
);
CREATE TABLE agent_step (
    id uuid PRIMARY KEY,
    run_id uuid NOT NULL REFERENCES agent_run(id),
    ordinal integer NOT NULL,
    stage text NOT NULL,
    tool_name text,
    evidence_refs jsonb NOT NULL DEFAULT '[]',
    summary text NOT NULL,
    duration_ms bigint NOT NULL,
    UNIQUE (run_id, ordinal)
);
CREATE TABLE llm_usage (
    id uuid PRIMARY KEY,
    run_id uuid NOT NULL REFERENCES agent_run(id),
    card_id uuid REFERENCES insight(id),
    stage text NOT NULL,
    model text NOT NULL,
    config_version text NOT NULL,
    attempt integer NOT NULL,
    input_tokens bigint NOT NULL,
    output_tokens bigint NOT NULL,
    reasoning_tokens bigint,
    actual_cost_rub numeric(18,8),
    duration_ms bigint NOT NULL,
    cache_hit boolean NOT NULL,
    error text,
    created_at timestamptz NOT NULL,
    request_key text NOT NULL DEFAULT '',
    reserved_cost_rub numeric(18,8) NOT NULL DEFAULT 0.0
);
CREATE INDEX usage_date_idx ON llm_usage(created_at);
CREATE INDEX usage_request_stage_idx ON llm_usage(request_key, stage);
CREATE TABLE llm_cache (
    cache_key text PRIMARY KEY,
    task text NOT NULL,
    model text NOT NULL,
    schema_version text NOT NULL,
    response_json jsonb NOT NULL,
    created_at timestamptz NOT NULL
);

CREATE TABLE user_activity (
    id uuid PRIMARY KEY,
    created_at timestamptz NOT NULL,
    user_id uuid NOT NULL REFERENCES app_user(id),
    company_id text NOT NULL REFERENCES company(id),
    insight_id uuid NOT NULL REFERENCES insight(id),
    kind text NOT NULL CHECK (kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted')),
    evidence_id text,
    action_id text,
    CHECK ((kind = 'source_opened') = (evidence_id IS NOT NULL)),
    CHECK ((kind = 'action_interacted') = (action_id IS NOT NULL))
);
CREATE INDEX activity_user_insight_idx ON user_activity(user_id, insight_id, created_at);
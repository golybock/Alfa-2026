export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };

export interface AcceptedResponse {
  readonly job_id: string;
  readonly status_url: string;
}

export interface AgentStepDto {
  readonly ordinal: number;
  readonly stage: string;
  readonly title: string;
  readonly summary: string;
  readonly status: AgentStepStatus;
  readonly duration_ms: number;
}

export type AgentStepStatus = "done" | "skipped" | "failed";

export type AnalysisStatus = "queued" | "processing" | "ready" | "failed";

export interface ApiError {
  readonly code: string;
  readonly message: string;
  readonly request_id: string;
}

export interface BankViewDto {
  readonly status: BankViewStatus;
  readonly claim_ids: ReadonlyArray<string>;
}

export type BankViewStatus = "found" | "not_found";

export interface ChartDraft {
  readonly local_id: string;
  readonly title: string;
  readonly description: string;
  readonly x_axis: string | null;
  readonly y_axis: string | null;
  readonly unit: string | null;
  readonly period: string | null;
  readonly legend: ReadonlyArray<string>;
  readonly points: ReadonlyArray<ChartPointDraft>;
  readonly region: ReadonlyArray<number>;
}

export interface ChartPointDraft {
  readonly series: string;
  readonly period: string;
  readonly value: number | null;
  readonly low: number | null;
  readonly high: number | null;
  readonly unit: string;
  readonly approximate: boolean;
  readonly reading_method: string;
}

export interface ChartReviewRequest {
  readonly corrected_chart: ChartDraft;
  readonly review_status: ReviewStatus;
}

export interface ChatAnswerDto {
  readonly id: string;
  readonly insight_id: string;
  readonly status: ChatAnswerStatus;
  readonly claims: ReadonlyArray<ClaimDto>;
  readonly evidence: Readonly<Record<string, EvidenceDto>>;
  readonly refusal_reason: string | null;
  readonly suggested_questions: ReadonlyArray<string>;
  readonly questions_remaining: number;
}

export type ChatAnswerStatus = "answer" | "refusal";

export interface ChatHistoryResponse {
  readonly items: ReadonlyArray<ChatTurnDto>;
}

export interface ChatRequest {
  readonly question: string;
}

export interface ChatTurnDto {
  readonly id: string;
  readonly question: string;
  readonly created_at: string;
  readonly answer: ChatAnswerDto;
}

export interface ChunkReviewRequest {
  readonly chunk_id: string;
  readonly corrected_text: string;
  readonly review_status: ReviewStatus;
  readonly corrected_tables: ReadonlyArray<TableDraft>;
  readonly charts: ReadonlyArray<ChartReviewRequest>;
  readonly note: string | null;
}

export interface ClaimDto {
  readonly id: string;
  readonly text: string;
  readonly origin: Origin;
  readonly refs: ReadonlyArray<string>;
  readonly verification_status: ClaimVerification;
  readonly confidence: Confidence | null;
}

export type ClaimVerification = "code_passed" | "critic_passed" | "not_confirmed";

export type CodeStatus = "passed" | "failed";

export interface CompanyAccessDto {
  readonly company_id: string;
  readonly name: string;
  readonly roles: ReadonlyArray<Role>;
}

export interface CompanyTraitDto {
  readonly code: string;
  readonly label: string;
  readonly amount_rub?: number | null;
}

export type Confidence = "low" | "medium" | "high";

export interface ConsequenceDto {
  readonly type: ConsequenceType;
  readonly claim_ids: ReadonlyArray<string>;
}

export type ConsequenceType = "risk" | "opportunity" | "neutral";

export type CriticStatus = "passed" | "issues_found" | "skipped_disabled" | "skipped_budget" | "skipped_error";

export interface CriticVerificationDto {
  readonly status: CriticStatus;
  readonly request_count: number;
  readonly reason: string | null;
}

export interface DocumentAcceptedResponse {
  readonly document_id: string;
  readonly job_id: string;
  readonly review_status: ReviewStatus;
}

export interface DocumentListResponse {
  readonly items: ReadonlyArray<DocumentSummaryDto>;
}

export interface DocumentPageDto {
  readonly page_no: number;
  readonly ocr_status: OcrStatus;
  readonly review_status: ReviewStatus;
  readonly page_image_url: string;
  readonly chunks: ReadonlyArray<ReviewableChunkDto>;
  readonly error: string | null;
}

export interface DocumentResponse {
  readonly id: string;
  readonly title: string;
  readonly published_on: string | null;
  readonly page_count: number;
  readonly version: string;
  readonly ocr_status: OcrStatus;
  readonly review_status: ReviewStatus;
  readonly pages: ReadonlyArray<DocumentPageDto>;
  readonly llm_key_required: boolean;
}

export interface DocumentSummaryDto {
  readonly id: string;
  readonly title: string;
  readonly published_on: string | null;
  readonly page_count: number;
  readonly version: string;
  readonly ocr_status: OcrStatus;
  readonly review_status: ReviewStatus;
  readonly uploaded_at: string;
  readonly uploaded_by_me: boolean;
  readonly llm_key_required: boolean;
}

export interface EvidenceDto {
  readonly type: EvidenceType;
  readonly source: string;
  readonly url: string | null;
  readonly quote: string | null;
  readonly date: string | null;
  readonly document_id: string | null;
  readonly page: number | null;
  readonly chunk_id: string | null;
  readonly key: string | null;
  readonly value: JsonValue | null;
  readonly dataset: string | null;
  readonly review_status: ReviewStatus | null;
  readonly approximate: boolean;
  readonly chart_id: string | null;
  readonly page_image_url: string | null;
}

export type EvidenceType = "news" | "bank_research" | "company_feature" | "payment_aggregate" | "group_feature";

export interface FeatureContribution {
  readonly code: RankingFeatureCode;
  readonly value: number;
  readonly weight: number;
  readonly contribution: number;
  readonly known: boolean;
  readonly evidence_refs: ReadonlyArray<string>;
}

export interface FeedItemDto {
  readonly event_id: string;
  readonly headline: string;
  readonly summary: string;
  readonly source: SourceSummaryDto;
  readonly relevance: RelevanceDto;
  readonly evidence: Readonly<Record<string, EvidenceDto>>;
  readonly analysis_status: AnalysisStatus;
  readonly insight_id: string | null;
  readonly job_id: string | null;
  readonly insight_headline?: string | null;
  readonly direction?: ConsequenceType | null;
  readonly queued_at?: string | null;
  readonly retry_available?: boolean;
}

export interface FeedResponse {
  readonly company_id: string;
  readonly as_of: string;
  readonly news_period_from: string;
  readonly news_period_to: string;
  readonly last_updated_at: string | null;
  readonly collection_started_at: string | null;
  readonly refresh_status: RefreshStatus;
  readonly items: ReadonlyArray<FeedItemDto>;
  readonly last_collected_at?: string | null;
  readonly collection_failed?: boolean;
}

export type FeedbackKind = "like" | "dislike" | "hide";

export type FeedbackReason = "not_relevant" | "wrong_fact" | "obvious";

export interface FeedbackRequest {
  readonly kind: FeedbackKind;
  readonly reason: FeedbackReason | null;
}

export interface ForecastDto {
  readonly status: ForecastStatus;
  readonly metric: string;
  readonly unit: string;
  readonly low: number | null;
  readonly high: number | null;
  readonly formula: string | null;
  readonly inputs: ReadonlyArray<ForecastInputDto>;
  readonly assumptions: ReadonlyArray<string>;
  readonly missing_data: ReadonlyArray<string>;
  readonly origin: Origin;
}

export interface ForecastInputDto {
  readonly name: string;
  readonly value: number;
  readonly unit: string;
  readonly assumed: boolean;
  readonly refs: ReadonlyArray<string>;
}

export type ForecastStatus = "estimated" | "insufficient_data";

export interface InsightDto {
  readonly id: string;
  readonly event_id: string;
  readonly company_id: string;
  readonly role: Role;
  readonly as_of: string;
  readonly headline: string;
  readonly relevance: RelevanceDto;
  readonly what_happened: ReadonlyArray<string>;
  readonly why_it_matters: ReadonlyArray<string>;
  readonly consequences: ReadonlyArray<ConsequenceDto>;
  readonly actions: ReadonlyArray<string>;
  readonly bank_view: BankViewDto;
  readonly claims: ReadonlyArray<ClaimDto>;
  readonly evidence: Readonly<Record<string, EvidenceDto>>;
  readonly limitations: ReadonlyArray<string>;
  readonly suggested_questions: ReadonlyArray<string>;
  readonly trace_id: string;
  readonly verification: VerificationDto;
  readonly forecast?: ForecastDto;
  readonly direction?: ConsequenceType | null;
  readonly news_period_from?: string | null;
  readonly news_period_to?: string | null;
  readonly company_traits?: ReadonlyArray<CompanyTraitDto> | null;
  readonly bank_data_as_of?: string | null;
}

export interface InsightRequest {
  readonly role: Role;
}

export interface InsightStepsResponse {
  readonly insight_id: string;
  readonly total_duration_ms: number;
  readonly steps: ReadonlyArray<AgentStepDto>;
}

export interface JobResponse {
  readonly id: string;
  readonly status: JobStatus;
  readonly stage: string;
  readonly result_type: string | null;
  readonly result_id: string | null;
  readonly error: ApiError | null;
}

export type JobStatus = "queued" | "running" | "done" | "failed";

export interface LoginRequest {
  readonly login: string;
  readonly password: string;
}

export interface ManualNewsItemDto {
  readonly job_id: string;
  readonly url: string;
  readonly title: string | null;
  readonly status: JobStatus;
  readonly stage: string;
  readonly error: ApiError | null;
  readonly created_at: string;
}

export interface ManualNewsListResponse {
  readonly items: ReadonlyArray<ManualNewsItemDto>;
}

export interface ManualNewsRequest {
  readonly source_id?: string | null;
  readonly url?: string | null;
  readonly title?: string | null;
  readonly text?: string | null;
  readonly published_at?: string | null;
}

export interface MeResponse {
  readonly user_id: string;
  readonly display_name: string;
  readonly is_operator: boolean;
  readonly companies: ReadonlyArray<CompanyAccessDto>;
  readonly can_add_materials: boolean;
}

export interface ModelConfigResponse {
  readonly deep_seek_model: string;
  readonly glm_model: string;
  readonly glm_reasoning: string;
  readonly general_max_tokens: number;
  readonly chat_max_tokens: number;
  readonly critic_enabled: boolean;
  readonly critic_scope: string;
}

export type OcrStatus = "queued" | "processing" | "ready" | "partial" | "failed";

export interface OperatorSourcesResponse {
  readonly items: ReadonlyArray<SourceHealthDto>;
}

export interface OperatorUsageResponse {
  readonly moscow_date: string;
  readonly daily_limit_rub: number;
  readonly available_rub: number;
  readonly stages: ReadonlyArray<StageUsageDto>;
}

export type Origin = "source_fact" | "bank_analytics" | "ai_inference" | "ai_hypothesis";

export interface PageReviewRequest {
  readonly chunks: ReadonlyArray<ChunkReviewRequest>;
}

export interface PendingInsightResponse {
  readonly status: AnalysisStatus;
  readonly job_id: string | null;
}

export type RankingFeatureCode = "okved" | "factor" | "product" | "country" | "cp" | "group";

export type RefreshStatus = "idle" | "queued" | "processing" | "failed";

export interface RelevanceDto {
  readonly score: number;
  readonly rank: number;
  readonly reasons: ReadonlyArray<RelevanceReasonDto>;
  readonly contributions: ReadonlyArray<FeatureContribution>;
  readonly significance?: Significance | null;
}

export interface RelevanceReasonDto {
  readonly code: string;
  readonly text: string;
  readonly claim_ids: ReadonlyArray<string>;
  readonly evidence_refs: ReadonlyArray<string>;
  readonly label?: string | null;
}

export type ResearchBlockKind = "fact" | "bank_view" | "forecast" | "disclaimer";

export interface ReviewAcceptedResponse {
  readonly document_id: string;
  readonly version: string;
  readonly job_id: string;
}

export type ReviewStatus = "not_reviewed" | "partially_reviewed" | "reviewed";

export interface ReviewableChartDto {
  readonly chart: ChartDraft;
  readonly review_status: ReviewStatus;
}

export interface ReviewableChunkDto {
  readonly id: string;
  readonly kind: ResearchBlockKind;
  readonly text: string;
  readonly review_status: ReviewStatus;
  readonly tables: ReadonlyArray<TableDraft>;
  readonly charts: ReadonlyArray<ReviewableChartDto>;
}

export type Role = "ceo" | "cfo";

export type Significance = "low" | "medium" | "high";

export interface SourceHealthDto {
  readonly id: string;
  readonly name: string;
  readonly feed_url: string;
  readonly enabled: boolean;
  readonly validation_status: string;
  readonly last_success_at: string | null;
  readonly last_error: string | null;
}

export interface SourceSummaryDto {
  readonly name: string;
  readonly url: string;
  readonly published_at: string;
}

export interface StageUsageDto {
  readonly stage: string;
  readonly requests: number;
  readonly known_cost_rub: number;
  readonly unknown_cost_requests: number;
  readonly duration_ms: number;
  readonly input_tokens: number;
  readonly output_tokens: number;
}

export interface TableDraft {
  readonly title: string;
  readonly columns: ReadonlyArray<string>;
  readonly rows: ReadonlyArray<ReadonlyArray<string | null>>;
}

export interface UserActivityRequest {
  readonly event_id: string;
  readonly type: UserActivityType;
  readonly evidence_id: string | null;
  readonly action_id: string | null;
}

export type UserActivityType = "insight_opened" | "reasons_opened" | "chat_question_accepted" | "source_opened" | "action_interacted" | "manager_contact_requested";

export interface VerificationDto {
  readonly code_status: CodeStatus;
  readonly critic: CriticVerificationDto;
  readonly repair_count: number;
}
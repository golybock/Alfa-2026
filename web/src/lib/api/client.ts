import type {
  AcceptedResponse,
  ApiError,
  ChatAnswerDto,
  ChatHistoryResponse,
  ChatRequest,
  DocumentListResponse,
  DocumentResponse,
  EvidenceDto,
  FeedbackRequest,
  FeedResponse,
  InsightDto,
  InsightStepsResponse,
  JobResponse,
  LoginRequest,
  MeResponse,
  PageReviewRequest,
  PendingInsightResponse,
  ReviewAcceptedResponse,
  Role,
  UserActivityRequest,
} from "../types/api";

export type Outcome<TReady, TWaiting> = { state: "ready"; data: TReady } | { state: "waiting"; data: TWaiting };

export type Api = {
  login(request: LoginRequest): Promise<MeResponse>;
  logout(): Promise<void>;
  me(): Promise<MeResponse>;
  getFeed(companyId: string, role: Role): Promise<FeedResponse>;
  refreshFeed(companyId: string): Promise<AcceptedResponse>;
  getInsight(companyId: string, eventId: string, role: Role): Promise<Outcome<InsightDto, PendingInsightResponse>>;
  ensureInsight(companyId: string, eventId: string, role: Role): Promise<Outcome<InsightDto, AcceptedResponse>>;
  getEvidence(insightId: string, evidenceId: string): Promise<EvidenceDto>;
  getJob(jobId: string): Promise<JobResponse>;
  getChatHistory(insightId: string): Promise<ChatHistoryResponse>;
  askQuestion(insightId: string, request: ChatRequest): Promise<Outcome<ChatAnswerDto, AcceptedResponse>>;
  sendFeedback(insightId: string, request: FeedbackRequest): Promise<void>;
  getInsightSteps(insightId: string): Promise<InsightStepsResponse>;
  recordActivity(insightId: string, request: UserActivityRequest): Promise<void>;
  listDocuments(): Promise<DocumentListResponse>;
  getDocument(documentId: string): Promise<DocumentResponse>;
  reviewPage(
    documentId: string,
    pageNo: number,
    version: string,
    request: PageReviewRequest,
  ): Promise<ReviewAcceptedResponse>;
};

export class ApiRequestError extends Error {
  readonly status: number;
  readonly error: ApiError;

  constructor(status: number, error: ApiError) {
    super(error.message);
    this.status = status;
    this.error = error;
  }
}

export function describeError(error: Error): string {
  return error instanceof ApiRequestError ? error.error.message : "Не удалось связаться с сервером";
}

const API_BASE = "/api";

type SentResponse = { status: number; body: unknown };

function isApiError(body: unknown): body is ApiError {
  return typeof body === "object" && body !== null && "code" in body && "message" in body;
}

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return null;
  }
  try {
    return await response.json();
  } catch {
    return null;
  }
}

async function send(
  method: "GET" | "POST",
  path: string,
  body?: unknown,
  headers: Record<string, string> = {},
): Promise<SentResponse> {
  const response = await fetch(`${API_BASE}${path}`, {
    method,
    credentials: "same-origin",
    headers: {
      Accept: "application/json",
      ...(body === undefined ? {} : { "Content-Type": "application/json" }),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const parsed = await readBody(response);

  if (!response.ok) {
    throw new ApiRequestError(
      response.status,
      isApiError(parsed) ? parsed : { code: "http_error", message: `Сервер ответил ошибкой ${response.status}`, request_id: "" },
    );
  }
  return { status: response.status, body: parsed };
}

function unexpected(status: number): ApiRequestError {
  return new ApiRequestError(status, {
    code: "unexpected_status",
    message: `Неожиданный ответ сервера ${status}`,
    request_id: "",
  });
}

export async function getJson<T>(path: string): Promise<T> {
  const { status, body } = await send("GET", path);
  if (status !== 200) {
    throw unexpected(status);
  }
  return body as T;
}

export async function postJson<T>(path: string, body?: unknown, headers?: Record<string, string>): Promise<T> {
  const response = await send("POST", path, body, headers);
  return response.body as T;
}

export async function postAccepted(path: string, body?: unknown, headers?: Record<string, string>): Promise<AcceptedResponse> {
  const { status, body: parsed } = await send("POST", path, body, headers);
  if (status !== 202) {
    throw unexpected(status);
  }
  return parsed as AcceptedResponse;
}

export async function postEmpty(path: string, body?: unknown): Promise<void> {
  const { status } = await send("POST", path, body);
  if (status !== 204) {
    throw unexpected(status);
  }
}

export async function getOutcome<TReady, TWaiting>(path: string): Promise<Outcome<TReady, TWaiting>> {
  return toOutcome<TReady, TWaiting>(await send("GET", path));
}

export async function postOutcome<TReady, TWaiting>(path: string, body?: unknown): Promise<Outcome<TReady, TWaiting>> {
  return toOutcome<TReady, TWaiting>(await send("POST", path, body));
}

function toOutcome<TReady, TWaiting>({ status, body }: SentResponse): Outcome<TReady, TWaiting> {
  if (status === 200) {
    return { state: "ready", data: body as TReady };
  }
  if (status === 202) {
    return { state: "waiting", data: body as TWaiting };
  }
  throw unexpected(status);
}

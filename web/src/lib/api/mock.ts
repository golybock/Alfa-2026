import chatAnswerJson from "../../../fixtures/api/chat-answer.json";
import chatRefusalJson from "../../../fixtures/api/chat-refusal.json";
import documentJson from "../../../fixtures/api/document.json";
import documentsListJson from "../../../fixtures/api/documents-list.json";
import feedCompanyBJson from "../../../fixtures/api/feed-company-b.json";
import feedEmptyJson from "../../../fixtures/api/feed-empty.json";
import feedPendingJson from "../../../fixtures/api/feed-pending.json";
import feedReadyJson from "../../../fixtures/api/feed-ready.json";
import insightCeoJson from "../../../fixtures/api/insight-ceo.json";
import insightCompanyBCeoJson from "../../../fixtures/api/insight-company-b-ceo.json";
import insightCompanyBJson from "../../../fixtures/api/insight-company-b.json";
import insightNoBankJson from "../../../fixtures/api/insight-no-bank.json";
import insightJson from "../../../fixtures/api/insight.json";
import jobDoneJson from "../../../fixtures/api/job-done.json";
import jobFailedJson from "../../../fixtures/api/job-failed.json";
import jobQueuedJson from "../../../fixtures/api/job-queued.json";
import meJson from "../../../fixtures/api/me.json";
import usersJson from "../../../fixtures/users.json";
import type {
  AcceptedResponse,
  ChatAnswerDto,
  ChatTurnDto,
  DocumentListResponse,
  DocumentResponse,
  EvidenceDto,
  FeedResponse,
  InsightDto,
  JobResponse,
  MeResponse,
} from "../types/api";
import type { Api } from "./client";
import { ApiRequestError } from "./client";

export type MockScenario = "pending" | "empty" | "no-bank" | "refusal" | "failed" | "chat-waiting" | "chat-failed" | "feed-1" | "feed-3" | "feed-5";

type MockCredentials = { login: string; password: string };

const MOCK_PAGE_IMAGE = "/mock-page.svg";
const QUESTION_LIMIT = 10;
const SESSION_STORAGE_KEY = "alfa.mock-session";

const me = meJson as MeResponse;
const credentials: MockCredentials[] = usersJson;
const jobQueued = jobQueuedJson as JobResponse;
const feeds: Record<string, FeedResponse> = {
  U_DEMO_A: feedReadyJson as FeedResponse,
  U_DEMO_B: feedCompanyBJson as FeedResponse,
};
const insights = [insightJson, insightCeoJson, insightCompanyBJson, insightCompanyBCeoJson] as InsightDto[];
const insightNoBank = insightNoBankJson as InsightDto;
const chatHistory = new Map<string, ChatTurnDto[]>();
const pendingChat = new Map<string, { insightId: string; turn: ChatTurnDto; polls: number }>();

const CHAT_JOB_POLLS = 2;
const feedItemCounts: Partial<Record<MockScenario, number>> = { "feed-1": 1, "feed-3": 3, "feed-5": 5 };

function currentScenario(): MockScenario | null {
  if (typeof window === "undefined") {
    return null;
  }
  const value = new URLSearchParams(window.location.search).get("mock");
  const known: MockScenario[] = ["pending", "empty", "no-bank", "refusal", "failed", "chat-waiting", "chat-failed", "feed-1", "feed-3", "feed-5"];
  return known.find((scenario) => scenario === value) ?? null;
}

function mockError(status: number, code: string, message: string): ApiRequestError {
  return new ApiRequestError(status, { code, message, request_id: "mock" });
}

function hasSession(): boolean {
  try {
    return window.localStorage.getItem(SESSION_STORAGE_KEY) === "active";
  } catch {
    return false;
  }
}

function setSession(active: boolean) {
  try {
    if (active) {
      window.localStorage.setItem(SESSION_STORAGE_KEY, "active");
    } else {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
    }
  } catch {
    return;
  }
}

function requireSession() {
  if (!hasSession()) {
    throw mockError(401, "unauthorized", "Сессия истекла, войдите снова");
  }
}

function requireCompany(companyId: string) {
  requireSession();
  if (!me.companies.some((company) => company.company_id === companyId)) {
    throw mockError(403, "forbidden_company", "Нет доступа к этой компании");
  }
}

function acceptedJob(): AcceptedResponse {
  return { job_id: jobQueued.id, status_url: `/api/jobs/${jobQueued.id}` };
}

function withMockPages(evidence: Readonly<Record<string, EvidenceDto>>): Record<string, EvidenceDto> {
  return Object.fromEntries(
    Object.entries(evidence).map(([id, item]) => [
      id,
      item.page_image_url ? { ...item, page_image_url: MOCK_PAGE_IMAGE } : item,
    ]),
  );
}

function findInsight(companyId: string, eventId: string, role: string): InsightDto | null {
  const insight = insights.find(
    (candidate) => candidate.company_id === companyId && candidate.event_id === eventId && candidate.role === role,
  );
  if (!insight) {
    return null;
  }
  const chosen = currentScenario() === "no-bank" && insight.id === insightNoBank.id ? insightNoBank : insight;
  return { ...chosen, evidence: withMockPages(chosen.evidence) };
}

function findInsightById(insightId: string): InsightDto {
  requireSession();
  const insight = [...insights, insightNoBank].find((candidate) => candidate.id === insightId);
  if (!insight) {
    throw mockError(404, "not_found", "Карточка не найдена");
  }
  return insight;
}

function feedFor(companyId: string): FeedResponse {
  const scenario = currentScenario();
  if (scenario === "empty") {
    return { ...(feedEmptyJson as FeedResponse), company_id: companyId };
  }
  if (scenario === "pending" && companyId === "U_DEMO_A") {
    return feedPendingJson as FeedResponse;
  }
  const itemCount = scenario ? feedItemCounts[scenario] : undefined;
  if (itemCount && companyId === "U_DEMO_A") {
    const base = feeds[companyId];
    const [first] = base.items;
    const items = Array.from({ length: itemCount }, (_, index) =>
      index === 0
        ? first
        : {
            ...first,
            event_id: `00000000-0000-4000-8000-0000000001${String(index).padStart(2, "0")}`,
            headline: `${first.headline} (${index + 1})`,
            relevance: { ...first.relevance, rank: index + 1 },
          },
    );
    return { ...base, items };
  }
  return feeds[companyId];
}

function answerFor(insightId: string, asked: number): ChatAnswerDto {
  const template = (currentScenario() === "refusal" ? chatRefusalJson : chatAnswerJson) as ChatAnswerDto;
  return {
    ...template,
    id: `${template.id}-${asked}`,
    insight_id: insightId,
    questions_remaining: QUESTION_LIMIT - asked,
  };
}

export const mockApi: Api = {
  login: async (request) => {
    const valid = credentials.some((user) => user.login === request.login && user.password === request.password);
    if (!valid) {
      throw mockError(401, "unauthorized", "Неверный логин или пароль");
    }
    setSession(true);
    return me;
  },
  logout: async () => {
    requireSession();
    setSession(false);
    chatHistory.clear();
  },
  me: async () => {
    requireSession();
    return me;
  },
  getFeed: async (companyId) => {
    requireCompany(companyId);
    return feedFor(companyId);
  },
  refreshFeed: async (companyId) => {
    requireCompany(companyId);
    return acceptedJob();
  },
  getInsight: async (companyId, eventId, role) => {
    requireCompany(companyId);
    if (currentScenario() === "pending") {
      return { state: "waiting", data: { status: "processing", job_id: jobQueued.id } };
    }
    const insight = findInsight(companyId, eventId, role);
    if (!insight) {
      throw mockError(404, "insight_not_prepared", "Анализ события ещё не подготовлен");
    }
    return { state: "ready", data: insight };
  },
  ensureInsight: async (companyId, eventId, role) => {
    requireCompany(companyId);
    const insight = findInsight(companyId, eventId, role);
    return insight ? { state: "ready", data: insight } : { state: "waiting", data: acceptedJob() };
  },
  getEvidence: async (insightId, evidenceId) => {
    const evidence = withMockPages(findInsightById(insightId).evidence)[evidenceId];
    if (!evidence) {
      throw mockError(404, "not_found", "Доказательство не найдено");
    }
    return evidence;
  },
  getJob: async (jobId) => {
    requireSession();
    const pending = pendingChat.get(jobId);
    if (pending) {
      pending.polls += 1;
      if (pending.polls < CHAT_JOB_POLLS) {
        return { ...jobQueued, id: jobId, status: "running", stage: "answer" };
      }
      pendingChat.delete(jobId);
      if (currentScenario() === "chat-failed") {
        return { ...(jobFailedJson as JobResponse), id: jobId };
      }
      chatHistory.set(pending.insightId, [...(chatHistory.get(pending.insightId) ?? []), pending.turn]);
      return { id: jobId, status: "done", stage: "saved", result_type: "chat", result_id: pending.turn.id, error: null };
    }
    return (currentScenario() === "failed" ? jobFailedJson : jobDoneJson) as JobResponse;
  },
  getChatHistory: async (insightId) => {
    findInsightById(insightId);
    return { items: chatHistory.get(insightId) ?? [] };
  },
  askQuestion: async (insightId, request) => {
    findInsightById(insightId);
    const history = chatHistory.get(insightId) ?? [];
    if (history.length >= QUESTION_LIMIT) {
      throw mockError(429, "question_limit", "Лимит вопросов по этой карточке исчерпан");
    }
    const answer = answerFor(insightId, history.length + 1);
    const turn: ChatTurnDto = {
      id: answer.id,
      question: request.question,
      created_at: new Date().toISOString(),
      answer,
    };
    if (currentScenario() === "chat-waiting" || currentScenario() === "chat-failed") {
      const jobId = crypto.randomUUID();
      pendingChat.set(jobId, { insightId, turn, polls: 0 });
      return { state: "waiting", data: { job_id: jobId, status_url: `/api/jobs/${jobId}` } };
    }
    chatHistory.set(insightId, [...history, turn]);
    return { state: "ready", data: answer };
  },
  sendFeedback: async (insightId) => {
    findInsightById(insightId);
  },
  listDocuments: async () => {
    requireSession();
    return documentsListJson as DocumentListResponse;
  },
  getDocument: async (documentId) => {
    requireSession();
    const document = documentJson as DocumentResponse;
    if (document.id !== documentId) {
      throw mockError(404, "not_found", "Документ не найден");
    }
    return {
      ...document,
      pages: document.pages.map((page) => ({ ...page, page_image_url: MOCK_PAGE_IMAGE })),
    };
  },
  reviewPage: async (documentId, _pageNo, version) => {
    const document = await mockApi.getDocument(documentId);
    if (document.version !== version) {
      throw mockError(409, "stale_review", "Документ изменился, обновите страницу");
    }
    return { document_id: documentId, version, job_id: jobQueued.id };
  },
};

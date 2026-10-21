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

export type MockScenario = "pending" | "empty" | "no-bank" | "refusal" | "failed";

const MOCK_PAGE_IMAGE = "/mock-page.svg";
const QUESTION_LIMIT = 10;

const me = meJson as MeResponse;
const jobQueued = jobQueuedJson as JobResponse;
const feeds: Record<string, FeedResponse> = {
  U_DEMO_A: feedReadyJson as FeedResponse,
  U_DEMO_B: feedCompanyBJson as FeedResponse,
};
const insights = [insightJson, insightCeoJson, insightCompanyBJson, insightCompanyBCeoJson] as InsightDto[];
const insightNoBank = insightNoBankJson as InsightDto;
const chatHistory = new Map<string, ChatTurnDto[]>();

function currentScenario(): MockScenario | null {
  if (typeof window === "undefined") {
    return null;
  }
  const value = new URLSearchParams(window.location.search).get("mock");
  const known: MockScenario[] = ["pending", "empty", "no-bank", "refusal", "failed"];
  return known.find((scenario) => scenario === value) ?? null;
}

function mockError(status: number, code: string, message: string): ApiRequestError {
  return new ApiRequestError(status, { code, message, request_id: "mock" });
}

function requireCompany(companyId: string) {
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
  login: async () => me,
  logout: async () => {
    chatHistory.clear();
  },
  me: async () => me,
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
  getJob: async () => (currentScenario() === "failed" ? jobFailedJson : jobDoneJson) as JobResponse,
  getChatHistory: async (insightId) => ({ items: chatHistory.get(insightId) ?? [] }),
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
    chatHistory.set(insightId, [...history, turn]);
    return { state: "ready", data: answer };
  },
  sendFeedback: async (insightId) => {
    findInsightById(insightId);
  },
  listDocuments: async () => documentsListJson as DocumentListResponse,
  getDocument: async (documentId) => {
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

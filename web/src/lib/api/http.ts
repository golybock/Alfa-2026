import type {
  AcceptedResponse,
  ChatAnswerDto,
  ChatHistoryResponse,
  DocumentListResponse,
  DocumentResponse,
  EvidenceDto,
  FeedResponse,
  InsightDto,
  JobResponse,
  MeResponse,
  PendingInsightResponse,
  ReviewAcceptedResponse,
} from "../types/api";
import type { Api } from "./client";
import { getJson, getOutcome, postAccepted, postEmpty, postJson, postOutcome } from "./client";

const segment = encodeURIComponent;

export const httpApi: Api = {
  login: (request) => postJson<MeResponse>("/auth/login", request),
  logout: () => postEmpty("/auth/logout"),
  me: () => getJson<MeResponse>("/me"),
  getFeed: (companyId, role) => getJson<FeedResponse>(`/companies/${segment(companyId)}/feed?role=${role}`),
  refreshFeed: (companyId) => postAccepted(`/companies/${segment(companyId)}/feed/refresh`),
  getInsight: (companyId, eventId, role) =>
    getOutcome<InsightDto, PendingInsightResponse>(
      `/companies/${segment(companyId)}/events/${segment(eventId)}/insight?role=${role}`,
    ),
  ensureInsight: (companyId, eventId, role) =>
    postOutcome<InsightDto, AcceptedResponse>(`/companies/${segment(companyId)}/events/${segment(eventId)}/insight`, {
      role,
    }),
  getEvidence: (insightId, evidenceId) =>
    getJson<EvidenceDto>(`/insights/${segment(insightId)}/evidence/${segment(evidenceId)}`),
  getJob: (jobId) => getJson<JobResponse>(`/jobs/${segment(jobId)}`),
  getChatHistory: (insightId) => getJson<ChatHistoryResponse>(`/insights/${segment(insightId)}/chat`),
  askQuestion: (insightId, request) =>
    postOutcome<ChatAnswerDto, AcceptedResponse>(`/insights/${segment(insightId)}/chat`, request),
  sendFeedback: (insightId, request) => postEmpty(`/insights/${segment(insightId)}/feedback`, request),
  listDocuments: () => getJson<DocumentListResponse>("/admin/documents"),
  getDocument: (documentId) => getJson<DocumentResponse>(`/admin/documents/${segment(documentId)}`),
  reviewPage: (documentId, pageNo, version, request) =>
    postJson<ReviewAcceptedResponse>(`/admin/documents/${segment(documentId)}/pages/${pageNo}/review`, request, {
      "If-Match": version,
    }),
};

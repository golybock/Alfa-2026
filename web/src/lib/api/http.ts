import type {
  AcceptedResponse,
  ChatAnswerDto,
  ChatHistoryResponse,
  DocumentAcceptedResponse,
  DocumentListResponse,
  ManualNewsListResponse,
  DocumentResponse,
  EvidenceDto,
  FeedResponse,
  InsightDto,
  InsightStepsResponse,
  JobResponse,
  MeResponse,
  NewsSourcesResponse,
  PendingInsightResponse,
  ReviewAcceptedResponse,
} from "../types/api";
import type { Api } from "./client";
import { getJson, getOutcome, postAccepted, postEmpty, postJson, postOutcome, uploadForm } from "./client";

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
  getInsightSteps: (insightId) => getJson<InsightStepsResponse>(`/insights/${segment(insightId)}/steps`),
  recordActivity: (insightId, request) => postEmpty(`/insights/${segment(insightId)}/activity`, request),
  addNews: (url) => postAccepted("/admin/news", { url }),
  listNews: () => getJson<ManualNewsListResponse>("/admin/news"),
  listNewsSources: () => getJson<NewsSourcesResponse>("/admin/news/sources"),
  uploadDocument: ({ file, title, publishedOn }, onProgress) => {
    const form = new FormData();
    form.append("file", file);
    form.append("title", title);
    if (publishedOn) {
      form.append("published_on", publishedOn);
    }
    return uploadForm<DocumentAcceptedResponse>("/admin/documents", form, onProgress);
  },
  listDocuments: () => getJson<DocumentListResponse>("/admin/documents"),
  getDocument: (documentId) => getJson<DocumentResponse>(`/admin/documents/${segment(documentId)}`),
  reviewPage: (documentId, pageNo, version, request) =>
    postJson<ReviewAcceptedResponse>(`/admin/documents/${segment(documentId)}/pages/${pageNo}/review`, request, {
      "If-Match": version,
    }),
};

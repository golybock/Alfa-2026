import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { QueryKey } from "@tanstack/react-query";
import { api } from "../api";
import type { ChatRequest, FeedbackRequest, Role, UserActivityRequest } from "../types/api";

const FEED_POLL_MS = 120_000;
const JOB_POLL_MS = 3_000;

export const queryKeys = {
  job: (jobId: string) => ["job", jobId] as const,
  feed: (companyId: string, role: Role) => ["feed", companyId, role] as const,
  insight: (companyId: string, eventId: string, role: Role) => ["insight", companyId, eventId, role] as const,
  chat: (insightId: string) => ["chat", insightId] as const,
  steps: (insightId: string) => ["steps", insightId] as const,
};

export function useFeed(companyId: string, role: Role, polling = true) {
  return useQuery({
    queryKey: queryKeys.feed(companyId, role),
    queryFn: () => api.getFeed(companyId, role),
    refetchInterval: polling ? FEED_POLL_MS : false,
  });
}

export function useRefreshFeed(companyId: string) {
  return useMutation({
    mutationFn: () => api.refreshFeed(companyId),
  });
}

export function useInsight(companyId: string, eventId: string, role: Role) {
  return useQuery({
    queryKey: queryKeys.insight(companyId, eventId, role),
    queryFn: () => api.getInsight(companyId, eventId, role),
  });
}

export function useChatHistory(insightId: string) {
  return useQuery({
    queryKey: queryKeys.chat(insightId),
    queryFn: () => api.getChatHistory(insightId),
  });
}

export function useJob(jobId: string | null, invalidateOnDone: QueryKey) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: queryKeys.job(jobId ?? ""),
    enabled: jobId !== null,
    queryFn: async () => {
      const job = await api.getJob(jobId ?? "");
      if (job.status === "done") {
        await queryClient.invalidateQueries({ queryKey: invalidateOnDone });
      }
      return job;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "done" || status === "failed" ? false : JOB_POLL_MS;
    },
  });
}

export function useAskQuestion(insightId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ChatRequest) => api.askQuestion(insightId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.chat(insightId) }),
  });
}

export function useSendFeedback(insightId: string, companyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: FeedbackRequest) => api.sendFeedback(insightId, request),
    onSuccess: (_, request) => {
      if (request.kind === "hide") {
        return queryClient.invalidateQueries({ queryKey: ["feed", companyId] });
      }
    },
  });
}

export function useInsightSteps(insightId: string) {
  return useQuery({
    queryKey: queryKeys.steps(insightId),
    queryFn: () => api.getInsightSteps(insightId),
  });
}

export function useRecordActivity(insightId: string) {
  return useMutation({
    mutationFn: (request: UserActivityRequest) => api.recordActivity(insightId, request),
  });
}

export function useRetryInsight(companyId: string, eventId: string, role: Role) {
  return useMutation({
    mutationFn: () => api.ensureInsight(companyId, eventId, role),
  });
}
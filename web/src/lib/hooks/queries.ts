import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { ChatRequest, Role } from "../types/api";

const FEED_POLL_MS = 120_000;

export const queryKeys = {
  feed: (companyId: string, role: Role) => ["feed", companyId, role] as const,
  insight: (companyId: string, eventId: string, role: Role) => ["insight", companyId, eventId, role] as const,
  chat: (insightId: string) => ["chat", insightId] as const,
};

export function useFeed(companyId: string, role: Role) {
  return useQuery({
    queryKey: queryKeys.feed(companyId, role),
    queryFn: () => api.getFeed(companyId, role),
    refetchInterval: FEED_POLL_MS,
  });
}

export function useRefreshFeed(companyId: string, role: Role) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.refreshFeed(companyId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.feed(companyId, role) }),
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

export function useAskQuestion(insightId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ChatRequest) => api.askQuestion(insightId, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.chat(insightId) }),
  });
}

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { ApiRequestError } from "../api/client";
import type { CompanyAccessDto, LoginRequest, MeResponse, Role } from "../types/api";

export type ScreenContext = { companyId: string; role: Role };

export type SessionState =
  | { status: "loading" }
  | { status: "unauthorized" }
  | { status: "failed"; error: Error }
  | { status: "unselected"; me: MeResponse }
  | { status: "forbidden"; me: MeResponse; context: ScreenContext }
  | { status: "ready"; me: MeResponse; company: CompanyAccessDto; context: ScreenContext };

export const ME_QUERY_KEY = ["me"] as const;

const CONTEXT_QUERY_KEY = ["screen-context"] as const;
const CONTEXT_STORAGE_KEY = "alfa.screen-context";
const companyScopedKeys = [["feed"], ["insight"], ["chat"]];

function isScreenContext(value: unknown): value is ScreenContext {
  return (
    typeof value === "object" &&
    value !== null &&
    "companyId" in value &&
    "role" in value &&
    typeof value.companyId === "string" &&
    (value.role === "ceo" || value.role === "cfo")
  );
}

function readStoredContext(): ScreenContext | null {
  try {
    const raw = window.localStorage.getItem(CONTEXT_STORAGE_KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : null;
    return isScreenContext(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function writeStoredContext(context: ScreenContext | null) {
  try {
    if (context) {
      window.localStorage.setItem(CONTEXT_STORAGE_KEY, JSON.stringify(context));
    } else {
      window.localStorage.removeItem(CONTEXT_STORAGE_KEY);
    }
  } catch {
    return;
  }
}

export function useMe() {
  return useQuery({ queryKey: ME_QUERY_KEY, queryFn: () => api.me() });
}

export function useSession(): SessionState {
  const me = useMe();
  const stored = useQuery({ queryKey: CONTEXT_QUERY_KEY, queryFn: readStoredContext, staleTime: Infinity });

  if (me.isPending || stored.isPending) {
    return { status: "loading" };
  }
  if (me.isError) {
    const unauthorized = me.error instanceof ApiRequestError && me.error.status === 401;
    return unauthorized ? { status: "unauthorized" } : { status: "failed", error: me.error };
  }
  const context = stored.data ?? null;
  if (!context) {
    return { status: "unselected", me: me.data };
  }
  const company = me.data.companies.find((candidate) => candidate.company_id === context.companyId);
  if (!company || !company.roles.includes(context.role)) {
    return { status: "forbidden", me: me.data, context };
  }
  return { status: "ready", me: me.data, company, context };
}

export function useActiveContext() {
  const session = useSession();
  return session.status === "ready" ? session : null;
}

export function useSelectContext() {
  const queryClient = useQueryClient();
  return (next: ScreenContext) => {
    const current = queryClient.getQueryData<ScreenContext | null>(CONTEXT_QUERY_KEY);
    if (current?.companyId !== next.companyId) {
      companyScopedKeys.forEach((queryKey) => queryClient.removeQueries({ queryKey }));
    }
    writeStoredContext(next);
    queryClient.setQueryData(CONTEXT_QUERY_KEY, next);
  };
}

export function useLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: LoginRequest) => api.login(request),
    onSuccess: (me) => {
      queryClient.clear();
      queryClient.setQueryData(ME_QUERY_KEY, me);
    },
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.logout(),
    onSettled: () => {
      writeStoredContext(null);
      queryClient.clear();
    },
  });
}

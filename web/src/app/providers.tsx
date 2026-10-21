"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { Query } from "@tanstack/react-query";
import { ApiRequestError } from "../lib/api/client";
import { ME_QUERY_KEY } from "../lib/hooks/session";

const MAX_RETRIES = 2;
const SESSION_LOST_URL = "/login?reason=session";

function shouldRetry(failureCount: number, error: Error) {
  const isClientError = error instanceof ApiRequestError && error.status < 500;
  return !isClientError && failureCount < MAX_RETRIES;
}

function isUnauthorized(error: Error) {
  return error instanceof ApiRequestError && error.status === 401 && window.location.pathname !== "/login";
}

function isSessionQuery(query: Query<unknown, unknown, unknown>) {
  return query.queryKey[0] === ME_QUERY_KEY[0];
}

function createQueryClient() {
  const leaveToLogin = () => {
    client.clear();
    window.location.replace(SESSION_LOST_URL);
  };
  const client: QueryClient = new QueryClient({
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (isUnauthorized(error) && !isSessionQuery(query)) {
          leaveToLogin();
        }
      },
    }),
    mutationCache: new MutationCache({
      onError: (error) => {
        if (isUnauthorized(error)) {
          leaveToLogin();
        }
      },
    }),
    defaultOptions: { queries: { retry: shouldRetry }, mutations: { retry: false } },
  });
  return client;
}

type ProvidersProps = { children: ReactNode };

export default function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(createQueryClient);

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

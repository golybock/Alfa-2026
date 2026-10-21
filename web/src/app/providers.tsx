"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiRequestError } from "../lib/api/client";

const MAX_RETRIES = 2;

function shouldRetry(failureCount: number, error: Error) {
  const isClientError = error instanceof ApiRequestError && error.status < 500;
  return !isClientError && failureCount < MAX_RETRIES;
}

type ProvidersProps = { children: ReactNode };

export default function Providers({ children }: ProvidersProps) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: shouldRetry }, mutations: { retry: false } } }),
  );

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

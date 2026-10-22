"use client";

import { useEffect } from "react";
import type { ReactNode } from "react";
import { useRouter } from "next/navigation";
import { describeError } from "../../lib/api/client";
import { useSession } from "../../lib/hooks/session";
import AccessDenied from "../auth/AccessDenied";
import AppShell from "./AppShell";

type AuthedShellProps = { children: ReactNode };

export default function AuthedShell({ children }: AuthedShellProps) {
  const router = useRouter();
  const session = useSession();

  useEffect(() => {
    if (session.status === "unauthorized") {
      router.replace("/login");
    }
    if (session.status === "unselected") {
      router.replace("/select");
    }
  }, [session.status, router]);

  if (session.status === "ready") {
    return (
      <AppShell me={session.me} company={session.company} role={session.context.role}>
        {children}
      </AppShell>
    );
  }
  if (session.status === "forbidden") {
    return <AccessDenied companyId={session.context.companyId} />;
  }
  if (session.status === "failed") {
    return (
      <p role="alert" className="p-6 text-sm text-alfa">
        Не удалось проверить сессию: {describeError(session.error)}
      </p>
    );
  }
  return <p className="p-6 text-sm text-zinc-500">Загрузка…</p>;
}

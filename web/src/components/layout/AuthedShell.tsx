import type { ReactNode } from "react";
import { requireCurrentUser } from "../../lib/api/auth";
import AppShell from "./AppShell";

type AuthedShellProps = { children: ReactNode };

export default async function AuthedShell({ children }: AuthedShellProps) {
  const user = await requireCurrentUser();

  return <AppShell user={user}>{children}</AppShell>;
}

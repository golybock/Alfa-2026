import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import users from "../../../fixtures/users.json";
import type { MockUserRecord, SessionUser } from "../types/auth";
import { requireMocks } from "./mode";

export const SESSION_COOKIE = "session";

const mockUsers = users as MockUserRecord[];

function toSessionUser(record: MockUserRecord): SessionUser {
  return {
    login: record.login,
    companyId: record.companyId,
    companyName: record.companyName,
    displayName: record.displayName,
    role: record.role,
  };
}

export function authenticate(login: string, password: string): SessionUser | null {
  requireMocks();
  const record = mockUsers.find((user) => user.login === login && user.password === password);
  return record ? toSessionUser(record) : null;
}

export async function getCurrentUser(): Promise<SessionUser | null> {
  const store = await cookies();
  requireMocks();
  const login = store.get(SESSION_COOKIE)?.value;
  const record = mockUsers.find((user) => user.login === login);
  return record ? toSessionUser(record) : null;
}

export async function requireCurrentUser(): Promise<SessionUser> {
  const user = await getCurrentUser();
  if (!user) {
    redirect("/login");
  }
  return user;
}

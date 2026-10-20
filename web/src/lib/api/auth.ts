import { cookies } from "next/headers";
import users from "../../../fixtures/users.json";
import type { MockUserRecord, SessionUser } from "../types/auth";

export const SESSION_COOKIE = "session";

const mockUsers: MockUserRecord[] = users;

function requireMocks() {
  if (process.env.NEXT_PUBLIC_USE_MOCKS !== "true") {
    throw new Error("Авторизация через API ещё не подключена: включите NEXT_PUBLIC_USE_MOCKS=true");
  }
}

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
  requireMocks();
  const store = await cookies();
  const login = store.get(SESSION_COOKIE)?.value;
  const record = mockUsers.find((user) => user.login === login);
  return record ? toSessionUser(record) : null;
}

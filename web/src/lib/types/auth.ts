import type { Role } from "./api";

export type SessionUser = {
  login: string;
  companyId: string;
  companyName: string;
  displayName: string;
  role: Role;
};

export type MockUserRecord = SessionUser & { password: string };

export type LoginState = { error: string | null; login: string };

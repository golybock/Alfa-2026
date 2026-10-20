export type SessionUser = {
  login: string;
  companyId: string;
  companyName: string;
  displayName: string;
  role: string;
};

export type MockUserRecord = SessionUser & { password: string };

export type LoginState = { error: string | null; login: string };

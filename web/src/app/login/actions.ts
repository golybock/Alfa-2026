"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { SESSION_COOKIE, authenticate } from "../../lib/api/auth";
import type { LoginState } from "../../lib/types/auth";

const SESSION_MAX_AGE_SECONDS = 60 * 60 * 8;

export async function loginAction(_state: LoginState, formData: FormData): Promise<LoginState> {
  const login = String(formData.get("login") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  if (!login || !password) {
    return { error: "Введите логин и пароль", login };
  }

  const user = authenticate(login, password);
  if (!user) {
    return { error: "Неверный логин или пароль", login };
  }

  const store = await cookies();
  store.set(SESSION_COOKIE, user.login, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.NODE_ENV === "production",
    path: "/",
    maxAge: SESSION_MAX_AGE_SECONDS,
  });
  redirect("/select");
}

export async function logoutAction() {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
  redirect("/login");
}

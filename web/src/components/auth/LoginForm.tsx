"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiRequestError, describeError } from "../../lib/api/client";
import { useLogin } from "../../lib/hooks/session";

function loginErrorText(error: Error): string {
  if (error instanceof ApiRequestError && error.status === 401) {
    return "Неверный логин или пароль";
  }
  if (error instanceof ApiRequestError && error.status === 429) {
    return "Слишком много попыток входа, попробуйте позже";
  }
  return describeError(error);
}

export default function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const login = useLogin();
  const [emptyFields, setEmptyFields] = useState(false);
  const sessionLost = searchParams.get("reason") === "session";

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const loginValue = String(form.get("login") ?? "").trim();
    const password = String(form.get("password") ?? "");
    if (!loginValue || !password) {
      setEmptyFields(true);
      return;
    }
    setEmptyFields(false);
    login.mutate({ login: loginValue, password }, { onSuccess: () => router.replace("/") });
  };

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-3">
      <p className="mb-1 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Введите логин и пароль</p>
      {sessionLost ? (
        <p role="status" className="rounded-xl bg-zinc-100 px-4 py-3 text-sm text-zinc-700">
          Сессия истекла. Войдите снова.
        </p>
      ) : null}
      <input
        name="login"
        type="text"
        aria-label="Логин"
        placeholder="Логин"
        autoComplete="username"
        required
        className="h-12 rounded-xl bg-canvas px-4 text-sm outline-none focus:ring-2 focus:ring-alfa/40"
      />
      <input
        name="password"
        type="password"
        aria-label="Пароль"
        placeholder="Пароль"
        autoComplete="current-password"
        required
        className="h-12 rounded-xl bg-canvas px-4 text-sm outline-none focus:ring-2 focus:ring-alfa/40"
      />
      {emptyFields ? (
        <p role="alert" className="text-sm text-alfa">
          Введите логин и пароль
        </p>
      ) : null}
      {login.isError ? (
        <p role="alert" className="text-sm text-alfa">
          {loginErrorText(login.error)}
        </p>
      ) : null}
      <button
        type="submit"
        disabled={login.isPending}
        className="mt-6 h-11 rounded-xl bg-alfa text-sm font-medium text-white transition-opacity disabled:opacity-60"
      >
        {login.isPending ? "Входим…" : "Войти"}
      </button>
    </form>
  );
}

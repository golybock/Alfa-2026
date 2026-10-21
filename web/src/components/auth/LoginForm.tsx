"use client";

import { useActionState } from "react";
import { loginAction } from "../../app/login/actions";
import type { LoginState } from "../../lib/types/auth";

const initialState: LoginState = { error: null, login: "" };

export default function LoginForm() {
  const [state, formAction, pending] = useActionState(loginAction, initialState);

  return (
    <form action={formAction} className="flex flex-col gap-3">
      <p className="mb-1 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Введите логин и пароль</p>
      <input
        name="login"
        type="text"
        aria-label="Логин"
        placeholder="Логин"
        defaultValue={state.login}
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
      {state.error ? (
        <p role="alert" className="text-sm text-alfa">
          {state.error}
        </p>
      ) : null}
      <button
        type="submit"
        disabled={pending}
        className="mt-6 h-11 rounded-xl bg-alfa text-sm font-medium text-white transition-opacity disabled:opacity-60"
      >
        {pending ? "Входим…" : "Войти"}
      </button>
    </form>
  );
}

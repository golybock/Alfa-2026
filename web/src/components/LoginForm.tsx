"use client";

import { useActionState } from "react";
import { loginAction } from "../app/login/actions";
import type { LoginState } from "../lib/types/auth";

const initialState: LoginState = { error: null, login: "" };

export default function LoginForm() {
  const [state, formAction, pending] = useActionState(loginAction, initialState);

  return (
    <form action={formAction} className="flex flex-col gap-4">
      <label htmlFor="login" className="flex flex-col gap-1 text-sm font-medium">
        Логин
        <input
          id="login"
          name="login"
          type="text"
          defaultValue={state.login}
          autoComplete="username"
          required
          className="h-12 rounded-lg border border-zinc-300 bg-white px-4 text-base text-zinc-900 outline-none focus:border-alfa"
        />
      </label>
      <label htmlFor="password" className="flex flex-col gap-1 text-sm font-medium">
        Пароль
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          className="h-12 rounded-lg border border-zinc-300 bg-white px-4 text-base text-zinc-900 outline-none focus:border-alfa"
        />
      </label>
      {state.error ? (
        <p role="alert" className="text-sm text-alfa">
          {state.error}
        </p>
      ) : null}
      <button
        type="submit"
        disabled={pending}
        className="h-12 rounded-lg bg-alfa text-base font-medium text-white transition-opacity disabled:opacity-60"
      >
        {pending ? "Входим…" : "Войти"}
      </button>
    </form>
  );
}

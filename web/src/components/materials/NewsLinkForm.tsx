"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { describeError } from "../../lib/api/client";
import { useAddNews } from "../../lib/hooks/queries";

type NewsLinkFormProps = { disabled: boolean };

export default function NewsLinkForm({ disabled }: NewsLinkFormProps) {
  const [url, setUrl] = useState("");
  const add = useAddNews();

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const value = url.trim();
    if (!value) {
      return;
    }
    add.mutate(value, { onSuccess: () => setUrl("") });
  };

  return (
    <form onSubmit={onSubmit} className="mt-6">
      <p className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">Или ссылка на новость</p>
      <div className="mt-2 flex gap-2">
        <input
          type="url"
          value={url}
          onChange={(event) => setUrl(event.target.value)}
          disabled={disabled || add.isPending}
          placeholder="https://"
          aria-label="Ссылка на новость"
          className="h-12 flex-1 rounded-xl border border-zinc-200 px-4 text-sm disabled:cursor-not-allowed disabled:bg-zinc-50"
        />
        <button
          type="submit"
          disabled={disabled || add.isPending || url.trim().length === 0}
          className="h-12 rounded-xl bg-alfa px-6 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {add.isPending ? "Добавляем…" : "Добавить"}
        </button>
      </div>
      {add.isError ? (
        <p role="alert" className="mt-2 text-xs text-alfa">
          {describeError(add.error)}
        </p>
      ) : null}
    </form>
  );
}
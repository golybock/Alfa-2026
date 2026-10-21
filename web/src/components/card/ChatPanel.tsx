"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { describeError } from "../../lib/api/client";
import { useAskQuestion, useChatHistory } from "../../lib/hooks/queries";
import type { InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ChatTurn from "./ChatTurn";

const MAX_QUESTION_LENGTH = 1000;

type ChatPanelProps = { insight: InsightDto };

export default function ChatPanel({ insight }: ChatPanelProps) {
  const [question, setQuestion] = useState("");
  const history = useChatHistory(insight.id);
  const ask = useAskQuestion(insight.id);
  const turns = history.data?.items ?? [];
  const lastTurn = turns.at(-1);
  const suggestions = lastTurn?.answer.suggested_questions ?? insight.suggested_questions;
  const remaining = lastTurn?.answer.questions_remaining ?? null;
  const exhausted = remaining === 0;

  const send = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || ask.isPending || exhausted) {
      return;
    }
    ask.mutate({ question: trimmed }, { onSuccess: () => setQuestion("") });
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    send(question);
  };

  return (
    <div className="flex flex-col gap-4">
      {history.isPending ? <p className="text-sm text-zinc-500">Загружаем историю…</p> : null}
      {history.isError ? (
        <p role="alert" className="text-sm text-alfa">
          Не удалось загрузить историю: {describeError(history.error)}
        </p>
      ) : null}
      {turns.length > 0 ? (
        <ul className="flex flex-col gap-4">
          {turns.map((turn) => (
            <ChatTurn key={turn.id} turn={turn} />
          ))}
        </ul>
      ) : null}
      {ask.isPending ? <p className="text-sm text-zinc-500">Готовим ответ…</p> : null}
      {ask.data?.state === "waiting" ? (
        <p className="text-sm text-zinc-500">Вопрос принят, ответ готовится в фоне.</p>
      ) : null}
      {ask.isError ? (
        <p role="alert" className="text-sm text-alfa">
          Вопрос не отправлен: {describeError(ask.error)}
        </p>
      ) : null}
      <ul className="flex flex-wrap gap-2">
        {suggestions.map((suggestion) => (
          <li key={suggestion}>
            <button
              type="button"
              onClick={() => send(suggestion)}
              disabled={ask.isPending || exhausted}
              className="rounded-xl bg-zinc-100 px-3 py-2 text-left text-xs hover:bg-zinc-200 disabled:opacity-60"
            >
              {suggestion}
            </button>
          </li>
        ))}
      </ul>
      <form onSubmit={onSubmit} className="flex items-center gap-2 rounded-xl border border-zinc-200 p-2 pl-4">
        <input
          type="text"
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          maxLength={MAX_QUESTION_LENGTH}
          disabled={exhausted}
          aria-label="Вопрос по сигналу"
          placeholder={exhausted ? "Лимит вопросов по карточке исчерпан" : "Задайте вопрос по сигналу"}
          className="min-w-0 flex-1 text-sm outline-none"
        />
        <button
          type="submit"
          aria-label="Отправить"
          disabled={ask.isPending || exhausted || question.trim().length === 0}
          className="flex h-10 w-10 items-center justify-center rounded-lg bg-alfa text-white disabled:opacity-60"
        >
          <Icon name="send" className="h-5 w-5" />
        </button>
      </form>
      {remaining !== null ? <p className="text-xs text-zinc-500">Осталось вопросов по карточке: {remaining}</p> : null}
      <p className="text-xs text-zinc-500">
        Ответы ИИ не являются позицией Альфа-Банка и индивидуальной инвестиционной рекомендацией.
      </p>
    </div>
  );
}

"use client";

import { useState } from "react";
import type { FormEvent } from "react";
import { describeError } from "../../lib/api/client";
import { queryKeys, useAskQuestion, useChatHistory, useJob } from "../../lib/hooks/queries";
import type { InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ChatTurn from "./ChatTurn";

const MAX_QUESTION_LENGTH = 1000;

type ChatPanelProps = { insight: InsightDto };

export default function ChatPanel({ insight }: ChatPanelProps) {
  const [question, setQuestion] = useState("");
  const history = useChatHistory(insight.id);
  const ask = useAskQuestion(insight.id);
  const [jobId, setJobId] = useState<string | null>(null);
  const job = useJob(jobId, queryKeys.chat(insight.id));
  const jobStatus = job.data?.status;
  const jobFinished = jobStatus === "done" || jobStatus === "failed";
  const awaitingJob = jobId !== null && !jobFinished && !job.isError;
  const busy = ask.isPending || awaitingJob;
  const turns = history.data?.items ?? [];
  const lastTurn = turns.at(-1);
  const suggestions = lastTurn?.answer.suggested_questions ?? insight.suggested_questions;
  const remaining = lastTurn?.answer.questions_remaining ?? null;
  const exhausted = remaining === 0;

  const send = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || busy || exhausted) {
      return;
    }
    ask.mutate(
      { question: trimmed },
      {
        onSuccess: (outcome) => {
          setQuestion("");
          setJobId(outcome.state === "waiting" ? outcome.data.job_id : null);
        },
      },
    );
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
      {busy ? (
        <p role="status" className="rounded-2xl bg-zinc-100 p-4 text-sm text-zinc-500">
          Готовим ответ…
        </p>
      ) : null}
      {jobStatus === "failed" ? (
        <p role="alert" className="text-sm text-alfa">
          Ответ не получен: {job.data?.error?.message ?? "обработка вопроса не удалась"}
        </p>
      ) : null}
      {job.isError ? (
        <p role="alert" className="text-sm text-alfa">
          Не удалось узнать статус ответа: {describeError(job.error)}
        </p>
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
              disabled={busy || exhausted}
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
          aria-label="Вопрос по инсайту"
          placeholder={exhausted ? "Лимит вопросов по карточке исчерпан" : "Задайте вопрос по инсайту"}
          className="min-w-0 flex-1 text-sm outline-none"
        />
        <button
          type="submit"
          aria-label="Отправить"
          disabled={busy || exhausted || question.trim().length === 0}
          className="flex h-10 w-10 items-center justify-center rounded-lg bg-alfa text-white disabled:opacity-60"
        >
          <Icon name="send" className="h-5 w-5" />
        </button>
      </form>
      {remaining !== null ? <p className="text-xs text-zinc-500">Осталось вопросов по карточке: {remaining}</p> : null}
      <p className="text-xs text-zinc-500">
        Ответы ИИ не являются позицией банка и инвестиционной рекомендацией.
      </p>
    </div>
  );
}

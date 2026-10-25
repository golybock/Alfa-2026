"use client";

import { useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import { describeError } from "../../lib/api/client";
import { queryKeys, useAskQuestion, useChatHistory, useJob } from "../../lib/hooks/queries";
import type { InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ChatTurn, { AssistantBubble, UserBubble } from "./ChatTurn";

const MAX_QUESTION_LENGTH = 1000;
const QUESTION_LIMIT = 10;

type ChatPanelProps = { insight: InsightDto };

// Вопрос показывается сразу после отправки и до тех пор, пока в истории не появится новый ход:
// известные ходы запоминаются по id, потому что повтор после сбоя убирает прежний ход из истории.
type Pending = { question: string; knownTurns: ReadonlySet<string> };

function Typing() {
  return (
    <span className="flex items-center gap-2 text-zinc-500">
      <span className="flex gap-1" aria-hidden="true">
        <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-zinc-400 [animation-delay:-0.3s]" />
        <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-zinc-400 [animation-delay:-0.15s]" />
        <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-zinc-400" />
      </span>
      Ищу ответ в материалах карточки…
    </span>
  );
}

export default function ChatPanel({ insight }: ChatPanelProps) {
  const [question, setQuestion] = useState("");
  const [pending, setPending] = useState<Pending | null>(null);
  const history = useChatHistory(insight.id);
  const ask = useAskQuestion(insight.id);
  const [jobId, setJobId] = useState<string | null>(null);
  const job = useJob(jobId, queryKeys.chat(insight.id));
  const endRef = useRef<HTMLDivElement>(null);
  const jobStatus = job.data?.status;
  const jobFinished = jobStatus === "done" || jobStatus === "failed";
  const awaitingJob = jobId !== null && !jobFinished && !job.isError;
  const turns = history.data?.items ?? [];
  const newTurnArrived = pending !== null && turns.some((turn) => !pending.knownTurns.has(turn.id));
  const jobFailedQuietly = jobStatus === "failed" && !history.isFetching;
  const showPending = pending !== null && !newTurnArrived && !jobFailedQuietly && !job.isError && !ask.isError;
  const busy = ask.isPending || awaitingJob || showPending;
  const lastTurn = turns.at(-1);
  const suggestions = lastTurn ? lastTurn.answer.suggested_questions : insight.suggested_questions;
  const remaining = lastTurn?.answer.questions_remaining ?? null;
  const exhausted = remaining === 0;

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end", behavior: "smooth" });
  }, [turns.length, showPending]);

  const send = (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || busy || exhausted) {
      return;
    }
    setPending({ question: trimmed, knownTurns: new Set(turns.map((turn) => turn.id)) });
    setJobId(null);
    ask.mutate(
      { question: trimmed },
      {
        onSuccess: (outcome) => {
          setQuestion("");
          if (outcome.state === "waiting") {
            setJobId(outcome.data.job_id);
          } else {
            setPending(null);
          }
        },
        onError: () => setPending(null),
      },
    );
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    send(question);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      send(question);
    }
  };

  return (
    <div className="flex min-h-[calc(100vh-9rem)] flex-col">
      <div className="flex flex-1 flex-col gap-4 pb-4">
        {history.isPending ? <p className="text-sm text-zinc-500">Загружаем историю…</p> : null}
        {history.isError ? (
          <p role="alert" className="text-sm text-alfa">
            Не удалось загрузить историю: {describeError(history.error)}
          </p>
        ) : null}
        {history.isSuccess && turns.length === 0 && !showPending ? (
          <AssistantBubble>
            <p>
              Спросите, что это событие значит для вашей компании. Отвечаю по материалам карточки и показываю, на чём основан
              ответ.
            </p>
          </AssistantBubble>
        ) : null}
        {turns.length > 0 ? (
          <ul className="flex flex-col gap-5">
            {turns.map((turn) => (
              <ChatTurn key={turn.id} turn={turn} onRetry={send} retryDisabled={busy || exhausted} />
            ))}
          </ul>
        ) : null}
        {showPending ? (
          <div role="status" className="flex flex-col gap-2">
            <UserBubble text={pending.question} />
            <AssistantBubble>
              <Typing />
            </AssistantBubble>
          </div>
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
        {!busy && !exhausted && suggestions.length > 0 ? (
          <ul className="flex flex-wrap gap-2">
            {suggestions.map((suggestion) => (
              <li key={suggestion}>
                <button
                  type="button"
                  onClick={() => send(suggestion)}
                  className="rounded-xl border border-zinc-200 px-3 py-2 text-left text-xs hover:bg-zinc-100"
                >
                  {suggestion}
                </button>
              </li>
            ))}
          </ul>
        ) : null}
        <div ref={endRef} />
      </div>
      <div className="sticky bottom-0 -mx-7 -mb-7 border-t border-zinc-100 bg-white px-7 pb-5 pt-3">
        <form onSubmit={onSubmit} className="flex items-end gap-2 rounded-xl border border-zinc-200 p-2 pl-4 focus-within:border-zinc-400">
          <textarea
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            onKeyDown={onKeyDown}
            maxLength={MAX_QUESTION_LENGTH}
            disabled={exhausted}
            rows={Math.min(5, Math.max(1, question.split("\n").length))}
            aria-label="Вопрос по инсайту"
            placeholder={exhausted ? "Лимит вопросов по карточке исчерпан" : "Задайте вопрос по инсайту"}
            className="min-w-0 flex-1 resize-none self-center py-2 text-sm leading-5 outline-none disabled:bg-white"
          />
          <button
            type="submit"
            aria-label="Отправить"
            disabled={busy || exhausted || question.trim().length === 0}
            className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-alfa text-white disabled:opacity-60"
          >
            <Icon name="send" className="h-5 w-5" />
          </button>
        </form>
        <p className="mt-2 flex flex-wrap justify-between gap-x-3 text-[11px] text-zinc-500">
          <span>Ответы ИИ не являются позицией банка и инвестиционной рекомендацией.</span>
          {remaining !== null ? (
            <span>
              Вопросов: {remaining} из {QUESTION_LIMIT}
            </span>
          ) : null}
        </p>
      </div>
    </div>
  );
}

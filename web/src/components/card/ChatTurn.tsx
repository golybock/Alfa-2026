import type { ReactNode } from "react";
import type { ChatAnswerDto, ChatTurnDto } from "../../lib/types/api";
import ClaimRow from "./ClaimRow";

type ChatTurnProps = { turn: ChatTurnDto; onRetry: (question: string) => void; retryDisabled: boolean };

export function UserBubble({ text }: { text: string }) {
  return (
    <p className="ml-auto max-w-[85%] whitespace-pre-line rounded-2xl rounded-br-md bg-zinc-900 px-4 py-2.5 text-sm leading-5 text-white">
      {text}
    </p>
  );
}

export function AssistantBubble({ children }: { children: ReactNode }) {
  return <div className="mr-auto w-full max-w-[92%] rounded-2xl rounded-bl-md bg-zinc-100 p-4 text-sm leading-6">{children}</div>;
}

function Basis({ answer }: { answer: ChatAnswerDto }) {
  return (
    <details className="group mt-3 border-t border-zinc-200 pt-3">
      <summary className="flex cursor-pointer list-none items-center gap-1 text-xs font-medium text-zinc-600 hover:text-zinc-900">
        <span className="transition-transform group-open:rotate-90">›</span>
        На чём основан ответ · {answer.claims.length}
      </summary>
      <ul className="@container mt-3 flex flex-col gap-3">
        {answer.claims.map((claim) => (
          <ClaimRow key={claim.id} claim={claim} evidence={answer.evidence} stacked />
        ))}
      </ul>
    </details>
  );
}

function Missing({ items }: { items: ReadonlyArray<string> }) {
  return (
    <div className="mt-3 rounded-xl bg-white px-3 py-2.5 text-xs leading-5 text-zinc-600">
      <p className="font-medium text-zinc-800">Для точного ответа не хватает данных</p>
      <ul className="mt-1 list-disc pl-4">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

export default function ChatTurn({ turn, onRetry, retryDisabled }: ChatTurnProps) {
  const { answer } = turn;
  const missing = answer.missing ?? [];

  return (
    <li className="flex flex-col gap-2">
      <UserBubble text={turn.question} />
      <AssistantBubble>
        {answer.status === "answer" ? (
          answer.answer ? (
            <>
              <p className="whitespace-pre-line">{answer.answer}</p>
              {missing.length > 0 ? <Missing items={missing} /> : null}
              {answer.claims.length > 0 ? <Basis answer={answer} /> : null}
            </>
          ) : (
            // Ответы до появления связного текста состоят только из утверждений.
            <ul className="@container flex flex-col gap-3">
              {answer.claims.map((claim) => (
                <ClaimRow key={claim.id} claim={claim} evidence={answer.evidence} stacked />
              ))}
            </ul>
          )
        ) : answer.status === "failed" ? (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-zinc-600">{answer.refusal_reason ?? "Не удалось подготовить ответ."}</p>
            <button
              type="button"
              onClick={() => onRetry(turn.question)}
              disabled={retryDisabled}
              className="rounded-lg bg-white px-3 py-1.5 text-xs font-medium hover:bg-zinc-200 disabled:opacity-60"
            >
              Повторить
            </button>
          </div>
        ) : (
          <>
            <p>{answer.refusal_reason ?? "Не могу ответить на этот вопрос."}</p>
            {missing.length > 0 ? <Missing items={missing} /> : null}
          </>
        )}
      </AssistantBubble>
    </li>
  );
}

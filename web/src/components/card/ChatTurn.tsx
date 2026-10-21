import type { ChatTurnDto } from "../../lib/types/api";
import ClaimRow from "./ClaimRow";

type ChatTurnProps = { turn: ChatTurnDto };

export default function ChatTurn({ turn }: ChatTurnProps) {
  const { answer } = turn;

  return (
    <li className="flex flex-col gap-3">
      <p className="ml-auto max-w-[85%] rounded-2xl bg-zinc-900 px-4 py-2.5 text-sm text-white">{turn.question}</p>
      <div className="rounded-2xl bg-zinc-100 p-4 text-sm leading-5">
        {answer.status === "refusal" ? (
          <>
            <p className="font-medium">Я помогаю только с анализом событий для вашей компании.</p>
            {answer.refusal_reason ? <p className="mt-1 text-xs text-zinc-500">{answer.refusal_reason}</p> : null}
          </>
        ) : (
          <ul className="@container flex flex-col gap-3">
            {answer.claims.map((claim) => (
              <ClaimRow key={claim.id} claim={claim} evidence={answer.evidence} />
            ))}
          </ul>
        )}
      </div>
    </li>
  );
}

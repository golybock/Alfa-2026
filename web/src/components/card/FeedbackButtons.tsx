"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { describeError } from "../../lib/api/client";
import { useSendFeedback } from "../../lib/hooks/queries";
import { useActiveContext } from "../../lib/hooks/session";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { FeedbackKind, FeedbackReason } from "../../lib/types/api";
import type { IconName } from "../../lib/types/icon";
import Icon from "../ui/Icon";

const actions: { kind: FeedbackKind; title: string; icon: IconName }[] = [
  { kind: "like", title: "Интересно", icon: "thumbUp" },
  { kind: "dislike", title: "Не интересно", icon: "thumbDown" },
  { kind: "hide", title: "Скрыть", icon: "eyeOff" },
];

const dislikeReasons: { reason: FeedbackReason; title: string }[] = [
  { reason: "not_relevant", title: "Не про нас" },
  { reason: "wrong_fact", title: "Неверный факт" },
  { reason: "obvious", title: "Очевидно" },
];

const feedbackAvailable = false;

const buttonClass =
  "flex h-8 items-center gap-1.5 rounded-lg border px-3 text-xs hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-transparent";

type FeedbackButtonsProps = { insightId: string };

export default function FeedbackButtons({ insightId }: FeedbackButtonsProps) {
  const router = useRouter();
  const companyId = useActiveContext()?.company.company_id ?? "";
  const feedback = useSendFeedback(insightId, companyId);
  const [chosen, setChosen] = useState<FeedbackKind | null>(null);
  const [askingReason, setAskingReason] = useState(false);

  const send = (kind: FeedbackKind, reason: FeedbackReason | null) => {
    feedback.mutate(
      { kind, reason },
      {
        onSuccess: () => {
          setChosen(kind);
          setAskingReason(false);
          if (kind === "hide") {
            router.push(ANALYTICS_HREF);
          }
        },
      },
    );
  };

  const choose = (kind: FeedbackKind) => {
    if (kind === "dislike") {
      setAskingReason(true);
      return;
    }
    setAskingReason(false);
    send(kind, null);
  };

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex gap-2">
        {actions.map((action) => (
          <button
            key={action.kind}
            type="button"
            disabled={!feedbackAvailable || feedback.isPending}
            title={feedbackAvailable ? undefined : "Пока недоступно"}
            aria-pressed={chosen === action.kind}
            onClick={() => choose(action.kind)}
            className={`${buttonClass} ${chosen === action.kind ? "border-zinc-900 bg-zinc-100" : "border-zinc-200"}`}
          >
            <Icon name={action.icon} className="h-3.5 w-3.5" />
            {action.title}
          </button>
        ))}
      </div>
      {askingReason ? (
        <div className="flex items-center gap-2">
          <p className="text-xs text-zinc-500">Почему?</p>
          {dislikeReasons.map((item) => (
            <button
              key={item.reason}
              type="button"
              disabled={feedback.isPending}
              onClick={() => send("dislike", item.reason)}
              className={`${buttonClass} border-zinc-200`}
            >
              {item.title}
            </button>
          ))}
          <button
            type="button"
            disabled={feedback.isPending}
            onClick={() => send("dislike", null)}
            className={`${buttonClass} border-zinc-200`}
          >
            Без причины
          </button>
        </div>
      ) : null}
      {feedback.isError ? (
        <p role="alert" className="text-xs text-red-600">
          Не удалось сохранить оценку: {describeError(feedback.error)}
        </p>
      ) : null}
    </div>
  );
}
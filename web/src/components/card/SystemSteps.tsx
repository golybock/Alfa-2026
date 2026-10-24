"use client";

import { ApiRequestError, describeError } from "../../lib/api/client";
import { formatDuration } from "../../lib/dates";
import { useInsightSteps } from "../../lib/hooks/queries";
import type { AgentStepStatus } from "../../lib/types/api";

const statusLabels: Record<Exclude<AgentStepStatus, "done">, string> = {
  skipped: "пропущено",
  failed: "не удалось",
};

type SystemStepsProps = { insightId: string };

export default function SystemSteps({ insightId }: SystemStepsProps) {
  const steps = useInsightSteps(insightId);

  if (steps.isPending) {
    return <p className="text-sm text-zinc-500">Загружаем шаги…</p>;
  }

  if (steps.error instanceof ApiRequestError && steps.error.status === 404) {
    return <p className="text-sm text-zinc-500">Данных нет</p>;
  }

  if (steps.isError) {
    return (
      <p role="alert" className="text-sm text-alfa">
        Не удалось загрузить шаги: {describeError(steps.error)}
      </p>
    );
  }

  if (steps.data.steps.length === 0) {
    return <p className="text-sm text-zinc-500">Данных нет</p>;
  }

  return (
    <>
      <ol className="flex flex-col gap-3 text-sm">
        {steps.data.steps.map((step) => (
          <li key={step.ordinal} className="flex items-start gap-3">
            <em className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-zinc-100 text-[11px] font-medium not-italic">
              {step.ordinal}
            </em>
            <div className="min-w-0 flex-1">
              <p className="font-medium">
                {step.title}
                {step.status === "done" ? null : (
                  <em className={`ml-2 text-xs not-italic ${step.status === "failed" ? "text-alfa" : "text-zinc-500"}`}>
                    {statusLabels[step.status]}
                  </em>
                )}
              </p>
              <p className="mt-0.5 text-xs text-zinc-500">{step.summary}</p>
            </div>
            <p className="shrink-0 text-xs text-zinc-500">{formatDuration(step.duration_ms)}</p>
          </li>
        ))}
      </ol>
      <p className="mt-3 text-xs text-zinc-500">Всего: {formatDuration(steps.data.total_duration_ms)}</p>
    </>
  );
}
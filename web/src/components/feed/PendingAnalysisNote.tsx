"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { describeError } from "../../lib/api/client";
import { formatTime } from "../../lib/dates";
import { queryKeys, useJob, useRetryInsight } from "../../lib/hooks/queries";
import { pendingAnalysisLabels } from "../../lib/labels";
import type { FeedItemDto, Role } from "../../lib/types/api";

type PendingAnalysisNoteProps = { item: FeedItemDto; companyId: string; role: Role };

export default function PendingAnalysisNote({ item, companyId, role }: PendingAnalysisNoteProps) {
  const queryClient = useQueryClient();
  const retry = useRetryInsight(companyId, item.event_id, role);
  const [jobId, setJobId] = useState<string | null>(null);
  const job = useJob(jobId, queryKeys.feed(companyId, role));
  const jobStatus = job.data?.status;
  const running = retry.isPending || (jobId !== null && jobStatus !== "done" && jobStatus !== "failed" && !job.isError);

  const onRetry = () =>
    retry.mutate(undefined, {
      onSuccess: (outcome) => {
        if (outcome.state === "ready") {
          queryClient.invalidateQueries({ queryKey: queryKeys.feed(companyId, role) });
          return;
        }
        setJobId(outcome.data.job_id);
      },
    });

  if (item.analysis_status === "failed") {
    return (
      <div className="flex max-w-56 flex-col items-end gap-2 text-right">
        <p className="text-xs text-alfa">
          {item.retry_available ? pendingAnalysisLabels.failed : "Не удалось подготовить карточку"}
        </p>
        {retry.isError ? <p className="text-xs text-alfa">{describeError(retry.error)}</p> : null}
        {jobStatus === "failed" ? <p className="text-xs text-alfa">Повтор не удался</p> : null}
        {item.retry_available ? (
          <button
            type="button"
            onClick={onRetry}
            disabled={running}
            className="h-9 rounded-xl border border-zinc-200 px-4 text-sm font-medium hover:bg-zinc-50 disabled:opacity-60"
          >
            {running ? "Повторяем…" : "Повторить"}
          </button>
        ) : null}
      </div>
    );
  }

  return (
    <div className="whitespace-nowrap text-right text-xs text-zinc-500">
      <p>Готовится</p>
      {item.queued_at ? (
        <>
          <p className="mt-0.5">в очереди с {formatTime(item.queued_at)}</p>
          <p className="mt-0.5">обычно около 3 минут</p>
        </>
      ) : null}
    </div>
  );
}
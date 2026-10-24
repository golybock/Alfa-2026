"use client";

import { formatTime } from "../../lib/dates";
import { useJob } from "../../lib/hooks/queries";
import Icon from "../ui/Icon";
import UploadStatusBadge from "./UploadStatusBadge";
import type { UploadStatus } from "./UploadStatusBadge";

export type LinkSubmission = { url: string; jobId: string; addedAt: string };

const feedKey = ["feed"] as const;

type LinkSubmissionRowProps = { submission: LinkSubmission };

export default function LinkSubmissionRow({ submission }: LinkSubmissionRowProps) {
  const job = useJob(submission.jobId, feedKey);
  const jobStatus = job.data?.status;
  const status: UploadStatus = jobStatus === "done" ? "ready" : jobStatus === "failed" ? "failed" : "processing";

  return (
    <li className="flex flex-col gap-2 border-t border-zinc-100 py-4">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-zinc-100">
          <Icon name="arrowUpRight" className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{submission.url}</p>
          <p className="text-xs text-zinc-500">Ссылка на новость · добавлено {formatTime(submission.addedAt)}</p>
        </div>
        <UploadStatusBadge status={status} readyLabel="Принято" />
      </div>
      {jobStatus === "done" ? (
        <p className="text-xs text-zinc-500">Новость обработана. Если она значима для вашей компании, она появится в ленте.</p>
      ) : null}
      {jobStatus === "failed" ? (
        <p className="text-xs text-alfa">{job.data?.error?.message ?? "Не удалось обработать новость."}</p>
      ) : null}
    </li>
  );
}
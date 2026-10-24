import type { OcrStatus } from "../../lib/types/api";
import Icon from "../ui/Icon";

export type UploadStatus = OcrStatus | "waiting_key";

const statuses: Record<UploadStatus, { label: string; className: string }> = {
  queued: { label: "В очереди", className: "bg-zinc-100 text-zinc-700" },
  processing: { label: "В обработке", className: "bg-blue-50 text-blue-700" },
  ready: { label: "Готово", className: "bg-green-50 text-green-700" },
  partial: { label: "Частично", className: "bg-amber-50 text-orange-700" },
  failed: { label: "Ошибка", className: "bg-red-50 text-alfa" },
  waiting_key: { label: "Ждёт ключа модели", className: "bg-amber-50 text-orange-700" },
};

type UploadStatusBadgeProps = { status: UploadStatus; readyLabel?: string };

export default function UploadStatusBadge({ status, readyLabel }: UploadStatusBadgeProps) {
  const { label, className } = statuses[status];

  return (
    <p className={`flex h-6 shrink-0 items-center gap-1 rounded-lg px-2 text-xs font-medium ${className}`}>
      {status === "ready" ? <Icon name="check" className="h-3.5 w-3.5" /> : null}
      {status === "processing" ? <i aria-hidden="true" className="h-1.5 w-1.5 animate-pulse rounded-full bg-blue-600" /> : null}
      {status === "ready" && readyLabel ? readyLabel : label}
    </p>
  );
}
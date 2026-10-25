import { formatTime } from "../../lib/dates";
import type { ManualNewsItemDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import UploadStatusBadge from "./UploadStatusBadge";
import type { UploadStatus } from "./UploadStatusBadge";

type LinkSubmissionRowProps = { item: ManualNewsItemDto };

export default function LinkSubmissionRow({ item }: LinkSubmissionRowProps) {
  const status: UploadStatus = item.status === "done" ? "ready" : item.status === "failed" ? "failed" : "processing";

  return (
    <li className="flex flex-col gap-2 border-t border-zinc-100 py-4">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-zinc-100">
          <Icon name="arrowUpRight" className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{item.title ?? item.url}</p>
          <p className="truncate text-xs text-zinc-500">
            Ссылка на новость · добавлено {formatTime(item.created_at)}
            {item.title ? ` · ${item.url}` : ""}
          </p>
        </div>
        <UploadStatusBadge status={status} readyLabel="Принято" />
      </div>
      {item.status === "done" ? (
        <p className="text-xs text-zinc-500">Новость обработана. Если она значима для вашей компании, она появится в ленте.</p>
      ) : null}
      {item.status === "failed" ? <p className="text-xs text-alfa">{item.error?.message ?? "Не удалось обработать новость."}</p> : null}
    </li>
  );
}
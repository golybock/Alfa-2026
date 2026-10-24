"use client";

import { formatDate } from "../../lib/dates";
import { useDocumentProgress } from "../../lib/hooks/queries";
import type { DocumentSummaryDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import UploadProgressBar from "./UploadProgressBar";
import UploadStatusBadge from "./UploadStatusBadge";

type UploadRowProps = { document: DocumentSummaryDto };

export default function UploadRow({ document }: UploadRowProps) {
  const inProgress = document.ocr_status === "queued" || document.ocr_status === "processing";
  const detail = useDocumentProgress(document.id, inProgress);
  const recognized = detail.data?.pages.filter((page) => page.ocr_status === "ready").length ?? 0;

  return (
    <li className="flex flex-col gap-2 border-t border-zinc-100 py-4">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-zinc-100">
          <Icon name="document" className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{document.title}</p>
          <p className="text-xs text-zinc-500">
            PDF · {document.page_count} стр.
            {document.published_on ? ` · ${formatDate(document.published_on)}` : ""}
          </p>
        </div>
        <UploadStatusBadge status={document.ocr_status} />
      </div>
      {inProgress ? (
        <UploadProgressBar
          fraction={document.page_count > 0 ? recognized / document.page_count : 0}
          label={`Распознаём страницы: ${recognized} из ${document.page_count}`}
        />
      ) : null}
      {document.ocr_status === "failed" ? <p className="text-xs text-alfa">Не удалось распознать документ.</p> : null}
    </li>
  );
}
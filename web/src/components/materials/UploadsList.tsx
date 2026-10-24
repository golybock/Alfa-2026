"use client";

import { describeError } from "../../lib/api/client";
import { useDocuments } from "../../lib/hooks/queries";
import LinkSubmissionRow from "./LinkSubmissionRow";
import type { LinkSubmission } from "./LinkSubmissionRow";
import UploadRow from "./UploadRow";

type UploadsListProps = { enabled: boolean; links: ReadonlyArray<LinkSubmission> };

export default function UploadsList({ enabled, links }: UploadsListProps) {
  const documents = useDocuments(enabled);
  const own = documents.data?.items
    .filter((item) => item.uploaded_by_me)
    .toSorted((first, second) => second.uploaded_at.localeCompare(first.uploaded_at));
  const empty = links.length === 0 && (own?.length ?? 0) === 0;

  return (
    <section aria-label="Ваши загрузки" className="rounded-2xl bg-white p-6">
      <h2 className="text-lg font-semibold">Ваши загрузки</h2>
      <p className="mt-1 text-xs text-zinc-500">Статусы обновляются автоматически</p>
      <div className="mt-3">
        {enabled && documents.isPending ? <p className="text-sm text-zinc-500">Загружаем список…</p> : null}
        {documents.isError ? (
          <p role="alert" className="text-sm text-alfa">
            Не удалось загрузить список: {describeError(documents.error)}
          </p>
        ) : null}
        {!documents.isPending && !documents.isError && empty ? <p className="text-sm text-zinc-500">Данных нет</p> : null}
        {links.length > 0 || (own?.length ?? 0) > 0 ? (
          <ul>
            {links.map((submission) => (
              <LinkSubmissionRow key={submission.jobId} submission={submission} />
            ))}
            {own?.map((document) => <UploadRow key={document.id} document={document} />)}
          </ul>
        ) : null}
      </div>
    </section>
  );
}
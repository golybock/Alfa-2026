"use client";

import { describeError } from "../../lib/api/client";
import { useDocuments, useNewsList } from "../../lib/hooks/queries";
import LinkSubmissionRow from "./LinkSubmissionRow";
import UploadRow from "./UploadRow";

type UploadsListProps = { enabled: boolean };

export default function UploadsList({ enabled }: UploadsListProps) {
  const documents = useDocuments(enabled);
  const news = useNewsList(enabled);
  const ownDocuments = documents.data?.items
    .filter((item) => item.uploaded_by_me)
    .toSorted((first, second) => second.uploaded_at.localeCompare(first.uploaded_at));
  const links = news.data?.items.toSorted((first, second) => second.created_at.localeCompare(first.created_at));
  const error = documents.error ?? news.error;
  const loading = enabled && (documents.isPending || news.isPending);
  const empty = (links?.length ?? 0) === 0 && (ownDocuments?.length ?? 0) === 0;

  return (
    <section aria-label="Ваши загрузки" className="rounded-2xl bg-white p-6">
      <h2 className="text-lg font-semibold">Ваши загрузки</h2>
      <p className="mt-1 text-xs text-zinc-500">Статусы обновляются автоматически</p>
      <div className="mt-3">
        {loading ? <p className="text-sm text-zinc-500">Загружаем список…</p> : null}
        {error ? (
          <p role="alert" className="text-sm text-alfa">
            Не удалось загрузить список: {describeError(error)}
          </p>
        ) : null}
        {!loading && !error && empty ? <p className="text-sm text-zinc-500">Данных нет</p> : null}
        {!empty ? (
          <ul>
            {links?.map((item) => <LinkSubmissionRow key={item.job_id} item={item} />)}
            {ownDocuments?.map((document) => <UploadRow key={document.id} document={document} />)}
          </ul>
        ) : null}
      </div>
    </section>
  );
}
"use client";

import { describeError } from "../../lib/api/client";
import { useDocuments } from "../../lib/hooks/queries";
import UploadRow from "./UploadRow";

type UploadsListProps = { enabled: boolean };

export default function UploadsList({ enabled }: UploadsListProps) {
  const documents = useDocuments(enabled);

  return (
    <section aria-label="Загруженные материалы" className="rounded-2xl bg-white p-6">
      <h2 className="text-lg font-semibold">Загруженные материалы</h2>
      <p className="mt-1 text-xs text-zinc-500">Статусы обновляются автоматически</p>
      <div className="mt-3">
        {!enabled ? <p className="text-sm text-zinc-500">Данных нет</p> : null}
        {enabled && documents.isPending ? <p className="text-sm text-zinc-500">Загружаем список…</p> : null}
        {documents.isError ? (
          <p role="alert" className="text-sm text-alfa">
            Не удалось загрузить список: {describeError(documents.error)}
          </p>
        ) : null}
        {documents.isSuccess && documents.data.items.length === 0 ? <p className="text-sm text-zinc-500">Данных нет</p> : null}
        {documents.isSuccess && documents.data.items.length > 0 ? (
          <ul>
            {documents.data.items.map((document) => (
              <UploadRow key={document.id} document={document} />
            ))}
          </ul>
        ) : null}
      </div>
    </section>
  );
}
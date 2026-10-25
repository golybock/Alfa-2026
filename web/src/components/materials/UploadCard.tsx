"use client";

import { useRef, useState } from "react";
import type { DragEvent } from "react";
import { describeError } from "../../lib/api/client";
import { useUploadDocument } from "../../lib/hooks/queries";
import Icon from "../ui/Icon";
import NewsLinkForm from "./NewsLinkForm";
import UploadProgressBar from "./UploadProgressBar";

const MAX_FILE_BYTES = 50 * 1024 * 1024;

type UploadCardProps = { disabled: boolean };

export default function UploadCard({ disabled }: UploadCardProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const upload = useUploadDocument();
  const [dragging, setDragging] = useState(false);
  const [fraction, setFraction] = useState<number | null>(null);
  const [fileName, setFileName] = useState("");
  const [rejection, setRejection] = useState<string | null>(null);

  const startUpload = (file: File) => {
    const isPdf = file.type === "application/pdf" || file.name.toLowerCase().endsWith(".pdf");
    if (!isPdf) {
      setRejection("Нужен файл в формате PDF.");
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      setRejection("Файл больше 50 МБ.");
      return;
    }
    setRejection(null);
    setFileName(file.name);
    setFraction(0);
    upload.mutate(
      { upload: { file, title: file.name.replace(/\.pdf$/i, ""), publishedOn: null }, onProgress: setFraction },
      { onSettled: () => setFraction(null) },
    );
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setDragging(false);
    const file = event.dataTransfer.files[0];
    if (file && !disabled && !upload.isPending) {
      startUpload(file);
    }
  };

  const busy = upload.isPending;
  const error = rejection ?? (upload.isError ? describeError(upload.error) : null);

  return (
    <section aria-label="Загрузка материала" className="rounded-2xl bg-white p-6">
      <div
        onDragOver={(event) => {
          event.preventDefault();
          if (!disabled) {
            setDragging(true);
          }
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={`flex min-h-52 flex-col items-center justify-center gap-3 rounded-2xl border border-dashed px-6 py-8 text-center transition-colors ${
          dragging ? "border-blue-600 bg-blue-50" : "border-zinc-300"
        } ${disabled ? "opacity-60" : ""}`}
      >
        <div className="flex h-12 w-12 items-center justify-center rounded-full bg-zinc-100">
          <Icon name="upload" className="h-5 w-5" />
        </div>
        {busy && fraction !== null ? (
          <div className="flex w-full max-w-md flex-col items-center gap-2">
            <p className="text-sm font-medium">{fileName}</p>
            <UploadProgressBar
              fraction={fraction}
              label={fraction >= 1 ? "Файл получен, регистрируем документ" : "Загрузка файла"}
            />
          </div>
        ) : (
          <>
            <p className="text-sm font-medium">Перетащите PDF сюда или выберите файл</p>
            <p className="text-xs text-zinc-500">PDF-скан, до 50 МБ и до 50 страниц</p>
            <button
              type="button"
              disabled={disabled}
              onClick={() => inputRef.current?.click()}
              className="h-11 rounded-xl bg-zinc-100 px-6 text-sm font-medium hover:bg-zinc-200 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Выбрать файл
            </button>
          </>
        )}
        <input
          ref={inputRef}
          type="file"
          accept="application/pdf,.pdf"
          hidden
          onChange={(event) => {
            const file = event.target.files?.[0];
            event.target.value = "";
            if (file) {
              startUpload(file);
            }
          }}
        />
      </div>
      {error ? (
        <p role="alert" className="mt-3 text-sm text-alfa">
          {error}
        </p>
      ) : null}
      {upload.isSuccess && !busy ? (
        <p role="status" className="mt-3 text-sm text-green-700">
          Файл принят и поставлен в обработку.
        </p>
      ) : null}
      <NewsLinkForm disabled={disabled} />
    </section>
  );
}
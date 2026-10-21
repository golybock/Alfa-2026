import Link from "next/link";
import type { ReactNode } from "react";
import Icon from "./Icon";

type ModalFrameProps = { title: string; step: 1 | 2; closeHref?: string; children: ReactNode };

export default function ModalFrame({ title, step, closeHref, children }: ModalFrameProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-900/50">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        className="w-[632px] rounded-3xl bg-white p-7 text-zinc-900 shadow-xl"
      >
        <div className="flex items-start justify-between">
          <h1 id="modal-title" className="text-2xl font-semibold">
            {title}
          </h1>
          {closeHref ? (
            <Link href={closeHref} aria-label="Закрыть" className="rounded-lg p-1 hover:bg-zinc-100">
              <Icon name="close" className="h-6 w-6" />
            </Link>
          ) : null}
        </div>
        <div className="mt-1 flex items-center gap-2 text-xs text-zinc-500">
          <p className="rounded-md bg-zinc-100 px-2 py-0.5 font-medium text-zinc-700">Демо-режим</p>
          <p>В банке эти данные система знает заранее</p>
        </div>
        <div className="mt-2 flex w-80 gap-1">
          <div className="h-1 flex-1 rounded-full bg-alfa" />
          <div className={`h-1 flex-1 rounded-full ${step === 2 ? "bg-alfa" : "bg-red-100"}`} />
        </div>
        <div className="mt-6">{children}</div>
      </section>
    </div>
  );
}

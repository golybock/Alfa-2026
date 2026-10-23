"use client";

import { useEffect, useRef } from "react";
import type { ReactNode } from "react";
import Icon from "./Icon";

type DrawerProps = { title: string; subtitle: string; onClose: () => void; children: ReactNode };

export default function Drawer({ title, subtitle, onClose, children }: DrawerProps) {
  const dialogRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const dialogs = document.querySelectorAll('[role="dialog"]');
      if (event.key === "Escape" && dialogs[dialogs.length - 1] === dialogRef.current) {
        onClose();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-zinc-900/40" onClick={onClose}>
      <aside
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        onClick={(event) => event.stopPropagation()}
        className="h-full w-[560px] overflow-y-auto rounded-l-3xl bg-white p-7 text-zinc-900 shadow-xl"
      >
        <div className="flex items-start justify-between gap-4">
          <h2 className="text-2xl font-semibold">{title}</h2>
          <button type="button" aria-label="Закрыть" onClick={onClose} className="rounded-lg p-1 hover:bg-zinc-100">
            <Icon name="close" className="h-6 w-6" />
          </button>
        </div>
        <p className="mt-1 text-xs text-zinc-500">{subtitle}</p>
        <div className="mt-5">{children}</div>
      </aside>
    </div>
  );
}

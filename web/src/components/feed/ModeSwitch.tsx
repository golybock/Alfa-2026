"use client";

import type { FeedMode } from "../../lib/feedMode";
import Icon from "../ui/Icon";

type ModeSwitchProps = { mode: FeedMode; snapshotDate: string; onChange: (mode: FeedMode) => void };

const segmentClass = (selected: boolean) =>
  `flex h-8 items-center gap-1.5 rounded-lg px-3 text-xs font-medium ${
    selected ? "bg-zinc-900 text-white" : "text-zinc-900 hover:bg-zinc-100"
  }`;

export default function ModeSwitch({ mode, snapshotDate, onChange }: ModeSwitchProps) {
  return (
    <div className="group relative">
      <div
        role="group"
        aria-label="Демо: режим ленты"
        className="flex h-11 items-center gap-1 rounded-xl border border-dashed border-blue-600 px-2"
      >
        <p className="px-1 text-xs text-blue-700">Демо · режим</p>
        <button type="button" aria-pressed={mode === "snapshot"} onClick={() => onChange("snapshot")} className={segmentClass(mode === "snapshot")}>
          <Icon name="camera" className="h-3.5 w-3.5" />
          Снимок на {snapshotDate}
        </button>
        <button type="button" aria-pressed={mode === "live"} onClick={() => onChange("live")} className={segmentClass(mode === "live")}>
          <i aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-green-500" />
          Живая лента
        </button>
      </div>
      <div
        role="tooltip"
        className="invisible absolute left-0 top-full z-20 mt-2 w-[360px] rounded-2xl bg-white p-4 text-xs shadow-xl group-focus-within:visible group-hover:visible"
      >
        <h2 className="text-sm font-semibold">Режим ленты</h2>
        <div className="mt-3 flex flex-col gap-3">
          <div>
            <p className="flex items-center gap-2 font-medium">
              Демо-снимок на {snapshotDate}
              <em className="rounded bg-zinc-100 px-1.5 py-0.5 text-[10px] font-medium not-italic text-zinc-600">По умолчанию</em>
            </p>
            <p className="mt-1 text-zinc-500">
              Данные заморожены: лента и карточки готовы заранее и не меняются. Надёжный режим для показа.
            </p>
          </div>
          <div>
            <p className="font-medium">Живая лента</p>
            <p className="mt-1 text-zinc-500">
              Текущая дата. Лента спрашивает сервер раз в 2 минуты, новые карточки могут считаться до двух минут.
            </p>
          </div>
        </div>
        <p className="mt-3 text-zinc-400">Переключатель нужен только для демо.</p>
      </div>
    </div>
  );
}
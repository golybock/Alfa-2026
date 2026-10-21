"use client";

import Link from "next/link";
import { describeError } from "../../lib/api/client";
import { useFeed } from "../../lib/hooks/queries";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { Role } from "../../lib/types/api";
import Icon from "../ui/Icon";
import InsightsWidgetRow from "./InsightsWidgetRow";

const WIDGET_ITEMS = 2;

type InsightsWidgetProps = { companyId: string; role: Role };

export default function InsightsWidget({ companyId, role }: InsightsWidgetProps) {
  const feed = useFeed(companyId, role);

  return (
    <section aria-labelledby="insights-title" className="mt-4 rounded-2xl bg-white p-5">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-pink-500 to-violet-600 text-white">
            <Icon name="trend" className="h-5 w-5" />
          </div>
          <div>
            <h2 id="insights-title" className="text-base font-semibold">
              Персональные бизнес-инсайты
            </h2>
            <p className="text-xs text-zinc-500">Это значимо именно для вашей компании</p>
          </div>
        </div>
        <Link href={ANALYTICS_HREF} className="flex h-10 items-center rounded-lg bg-alfa px-5 text-sm font-medium text-white">
          Все инсайты
        </Link>
      </div>
      {feed.isPending ? <p className="mt-4 rounded-xl bg-zinc-100 p-5 text-sm text-zinc-500">Загружаем…</p> : null}
      {feed.isError ? (
        <p role="alert" className="mt-4 rounded-xl bg-zinc-100 p-5 text-sm text-alfa">
          Не удалось загрузить сигналы: {describeError(feed.error)}
        </p>
      ) : null}
      {feed.isSuccess && feed.data.items.length === 0 ? (
        <p className="mt-4 rounded-xl bg-zinc-100 p-5 text-center text-sm text-zinc-500">
          Значимых событий за неделю нет
        </p>
      ) : null}
      {feed.isSuccess && feed.data.items.length > 0 ? (
        <ul className="mt-4 flex flex-col gap-2">
          {feed.data.items.slice(0, WIDGET_ITEMS).map((item) => (
            <InsightsWidgetRow key={item.event_id} item={item} />
          ))}
        </ul>
      ) : null}
    </section>
  );
}

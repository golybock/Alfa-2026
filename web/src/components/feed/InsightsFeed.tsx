"use client";

import { describeError } from "../../lib/api/client";
import { formatDateTime, formatPeriod } from "../../lib/dates";
import { useFeed, useRefreshFeed } from "../../lib/hooks/queries";
import { roleLabels } from "../../lib/labels";
import type { FeedResponse, RefreshStatus, Role } from "../../lib/types/api";
import EmptyFeed from "./EmptyFeed";
import FeedHeader from "./FeedHeader";
import InsightRow from "./InsightRow";

const refreshStatusLabels: Record<Exclude<RefreshStatus, "idle">, string> = {
  queued: "сбор новостей в очереди",
  processing: "идёт сбор новостей",
  failed: "последний сбор завершился ошибкой",
};

function feedStatusLine(feed: FeedResponse): string {
  const parts = [`Новости за ${formatPeriod(feed.news_period_from, feed.news_period_to)}`];
  parts.push(feed.last_updated_at ? `обновлено ${formatDateTime(feed.last_updated_at)}` : "лента ещё не собиралась");
  if (feed.collection_started_at && new Date(feed.collection_started_at) > new Date(feed.news_period_from)) {
    parts.push(`сбор идёт с ${formatDateTime(feed.collection_started_at)}`);
  }
  if (feed.refresh_status !== "idle") {
    parts.push(refreshStatusLabels[feed.refresh_status]);
  }
  return parts.join(" · ");
}

type InsightsFeedProps = { companyId: string; companyName: string; role: Role };

export default function InsightsFeed({ companyId, companyName, role }: InsightsFeedProps) {
  const feed = useFeed(companyId, role);
  const refresh = useRefreshFeed(companyId, role);

  return (
    <>
      <FeedHeader
        subtitle={`${companyName} · ${roleLabels[role]}`}
        refreshing={refresh.isPending}
        onRefresh={() => refresh.mutate()}
      />
      {refresh.isError ? (
        <p role="alert" className="mb-3 text-sm text-alfa">
          Не удалось запустить обновление: {describeError(refresh.error)}. Показана последняя сохранённая лента.
        </p>
      ) : null}
      {refresh.isSuccess ? <p className="mb-3 text-sm text-zinc-500">Запрос на обновление принят.</p> : null}
      {feed.isPending ? <p className="rounded-2xl bg-white p-5 text-sm text-zinc-500">Загружаем ленту…</p> : null}
      {feed.isError ? (
        <div role="alert" className="rounded-2xl bg-white p-5 text-sm">
          <p className="text-alfa">Не удалось загрузить ленту: {describeError(feed.error)}</p>
          <button
            type="button"
            onClick={() => feed.refetch()}
            className="mt-3 h-9 rounded-xl bg-zinc-100 px-4 text-sm font-medium hover:bg-zinc-200"
          >
            Повторить
          </button>
        </div>
      ) : null}
      {feed.isSuccess ? (
        <section>
          <p className="text-xs text-zinc-500">{feedStatusLine(feed.data)}</p>
          {feed.data.items.length === 0 ? (
            <EmptyFeed
              companyName={companyName}
              period={formatPeriod(feed.data.news_period_from, feed.data.news_period_to)}
            />
          ) : (
            <ul className="mt-4 flex flex-col gap-3">
              {feed.data.items.map((item) => (
                <InsightRow key={item.event_id} item={item} />
              ))}
            </ul>
          )}
          <p className="mt-4 text-xs text-zinc-500">
            Порядок отражает предполагаемую значимость события для компании, а не измеренную силу влияния.
            Информация не является индивидуальной инвестиционной рекомендацией.
          </p>
        </section>
      ) : null}
    </>
  );
}

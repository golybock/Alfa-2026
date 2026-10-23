"use client";

import { useState } from "react";
import { describeError } from "../../lib/api/client";
import { formatAgo, formatDateTimeWithYear, formatDateWithYear, formatDayMonthNumeric, formatPeriod } from "../../lib/dates";
import type { FeedMode } from "../../lib/feedMode";
import { queryKeys, useFeed, useJob, useRefreshFeed } from "../../lib/hooks/queries";
import { useNow } from "../../lib/hooks/useNow";
import type { FeedItemDto, FeedResponse, Role } from "../../lib/types/api";
import EmptyFeed from "./EmptyFeed";
import FeedHeader from "./FeedHeader";
import FeedNotice from "./FeedNotice";
import FeedProgress from "./FeedProgress";
import InsightRow from "./InsightRow";

const MAX_FEED_ITEMS = 5;
const AGO_TICK_MS = 10_000;

function newInsightsTitle(count: number): string {
  const last = count % 10;
  const tail = count % 100;
  if (last === 1 && tail !== 11) {
    return `Появился ${count} новый инсайт`;
  }
  return last >= 2 && last <= 4 && (tail < 12 || tail > 14) ? `Появились ${count} новых инсайта` : `Появилось ${count} новых инсайтов`;
}

function subtitleOf(feed: FeedResponse, companyName: string, mode: FeedMode, updatedAt: number, now: number): string {
  const parts = [companyName];
  if (mode === "snapshot") {
    parts.push(`снимок данных на ${formatDateWithYear(feed.as_of)}`);
  } else {
    parts.push(`данные на ${formatDateTimeWithYear(feed.as_of)}${feed.refresh_status === "failed" ? " (из кэша)" : ""}`);
    parts.push(`обновлено ${formatAgo(updatedAt, now)}`);
  }
  if (feed.last_updated_at === null) {
    parts.push("лента ещё не собиралась");
  }
  parts.push(`окно 7 дней: ${formatPeriod(feed.news_period_from, feed.news_period_to)}`);
  return parts.join(" · ");
}

type InsightsFeedProps = { companyId: string; companyName: string; role: Role };

export default function InsightsFeed({ companyId, companyName, role }: InsightsFeedProps) {
  const [mode, setMode] = useState<FeedMode>("snapshot");
  const [acknowledged, setAcknowledged] = useState<ReadonlyArray<string> | null>(null);
  const live = mode === "live";
  const feed = useFeed(companyId, role, live);
  const refresh = useRefreshFeed(companyId);
  const [refreshJobId, setRefreshJobId] = useState<string | null>(null);
  const refreshJob = useJob(refreshJobId, queryKeys.feed(companyId, role));
  const refreshJobStatus = refreshJob.data?.status;
  const refreshRunning = refreshJobId !== null && refreshJobStatus !== "done" && refreshJobStatus !== "failed" && !refreshJob.isError;
  const now = useNow(AGO_TICK_MS);

  const items: ReadonlyArray<FeedItemDto> = feed.data?.items.slice(0, MAX_FEED_ITEMS) ?? [];
  const currentIds = items.map((item) => item.event_id);
  if (live && feed.isSuccess && acknowledged === null) {
    setAcknowledged(currentIds);
  }
  const knownIds = live ? acknowledged : null;
  const shown = knownIds ? items.filter((item) => knownIds.includes(item.event_id)) : items;
  const fresh = knownIds ? items.filter((item) => !knownIds.includes(item.event_id)) : [];
  const generating = feed.data?.refresh_status === "queued" || feed.data?.refresh_status === "processing";

  const changeMode = (next: FeedMode) => {
    setMode(next);
    setAcknowledged(null);
  };

  return (
    <>
      <FeedHeader
        subtitle={feed.isSuccess ? subtitleOf(feed.data, companyName, mode, feed.dataUpdatedAt, now) : companyName}
        mode={mode}
        snapshotDate={feed.isSuccess ? formatDayMonthNumeric(feed.data.as_of) : "—"}
        onModeChange={changeMode}
        refreshing={refresh.isPending || refreshRunning}
        onRefresh={() => refresh.mutate(undefined, { onSuccess: (accepted) => setRefreshJobId(accepted.job_id) })}
      />
      <div className="flex flex-col gap-3">
        {refresh.isError ? (
          <FeedNotice
            tone="warning"
            title="Не удалось запустить обновление"
            text={`${describeError(refresh.error)}. Показана последняя сохранённая лента.`}
          />
        ) : null}
        {refreshRunning ? (
          <p role="status" className="text-sm text-zinc-500">
            Обновляем ленту. Пока показана последняя сохранённая версия.
          </p>
        ) : null}
        {refreshJobStatus === "failed" || refreshJob.isError ? (
          <FeedNotice
            tone="warning"
            title="Обновление не удалось"
            text={`${refreshJob.data?.error?.message ?? "Не получен статус обновления."} Показана последняя сохранённая лента.`}
          />
        ) : null}
        {feed.data?.refresh_status === "failed" ? (
          <FeedNotice
            tone="warning"
            title="Не удалось получить свежие новости"
            text={`Показываем сохранённые инсайты на ${formatDateTimeWithYear(feed.data.as_of)}. Попробуем ещё раз автоматически.`}
          />
        ) : null}
        {fresh.length > 0 ? (
          <FeedNotice
            tone="info"
            title={newInsightsTitle(fresh.length)}
            text={`Лента сама спрашивает сервер раз в 2 минуты и не требует перезагрузки страницы. Новый инсайт: ${fresh[0].headline}`}
            actionLabel="Показать"
            onAction={() => setAcknowledged(currentIds)}
          />
        ) : null}
        {generating ? <FeedProgress companyName={companyName} /> : null}
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
      </div>
      {feed.isSuccess ? (
        <section>
          {shown.length === 0 ? (
            generating ? null : (
              <EmptyFeed
                companyName={companyName}
                period={formatPeriod(feed.data.news_period_from, feed.data.news_period_to)}
              />
            )
          ) : (
            <>
              <div className="mt-4 flex items-center justify-between gap-4">
                <p className="flex h-9 items-center rounded-xl bg-zinc-900 px-4 text-xs font-medium text-white">
                  Все инсайты · {shown.length}
                </p>
                <p className="text-xs text-zinc-500">Порядок: по значимости для вашей компании</p>
              </div>
              <ul className="mt-3 flex flex-col gap-3">
                {shown.map((item) => (
                  <InsightRow key={item.event_id} item={item} />
                ))}
              </ul>
            </>
          )}
          <p className="mt-4 text-xs text-zinc-500">
            Показаны только инсайты, значимые именно для {companyName}. Аналитика не является инвестиционной
            рекомендацией, выводы ИИ не являются позицией Альфа-Банка.
          </p>
        </section>
      ) : null}
    </>
  );
}
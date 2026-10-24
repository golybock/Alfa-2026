"use client";

import Link from "next/link";
import { formatDateNumeric, formatDateWithYear, formatPeriod } from "../../lib/dates";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ImpactBadge from "../ui/ImpactBadge";
import KindBadge from "../ui/KindBadge";
import RoleSwitch from "../ui/RoleSwitch";
import FeedbackButtons from "./FeedbackButtons";

type InsightCardHeaderProps = { insight: InsightDto; onOpenChat: () => void };

export default function InsightCardHeader({ insight, onOpenChat }: InsightCardHeaderProps) {
  const firstFact = insight.claims.find((claim) => claim.id === insight.what_happened[0]);
  const newsSource =
    firstFact?.refs.map((ref) => insight.evidence[ref]).find((item) => item?.type === "news" && item.url) ?? null;
  const periodFrom = insight.news_period_from ?? null;
  const periodTo = insight.news_period_to ?? null;

  return (
    <header className="rounded-2xl bg-white p-5">
      <div className="flex items-start justify-between gap-6">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Link href={ANALYTICS_HREF} className="flex items-center gap-1 text-xs text-zinc-600 hover:text-zinc-900">
              <Icon name="chevronLeft" className="h-3.5 w-3.5" />
              Все инсайты
            </Link>
            {insight.direction ? <KindBadge kind={insight.direction} /> : null}
            {insight.relevance.significance ? <ImpactBadge significance={insight.relevance.significance} /> : null}
            {insight.verification.critic.status.startsWith("skipped_") ? (
              <p className="flex h-6 items-center gap-1 rounded-lg bg-amber-50 px-2 text-xs font-medium text-orange-700">
                <Icon name="info" className="h-3.5 w-3.5" />
                Без проверки критиком
              </p>
            ) : null}
            {periodFrom && periodTo ? (
              <p className="text-xs text-zinc-500">
                Период анализа: {formatPeriod(periodFrom, periodTo)} {formatDateWithYear(periodTo).split(" ").at(-1)}
              </p>
            ) : null}
          </div>
          <h1 className="mt-3 text-3xl font-semibold leading-tight">{insight.headline}</h1>
          {firstFact ? (
            <p className="mt-2 flex flex-wrap items-center gap-x-3 text-sm text-zinc-600">
              {firstFact.text}
              {newsSource?.url ? (
                <a
                  href={newsSource.url}
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-1 text-blue-600 hover:underline"
                >
                  <Icon name="arrowUpRight" className="h-3.5 w-3.5" />
                  {newsSource.source}
                  {newsSource.date ? ` · ${formatDateNumeric(newsSource.date)}` : ""}
                </a>
              ) : null}
            </p>
          ) : null}
        </div>
        <div className="flex shrink-0 flex-col items-end gap-3">
          <button
            type="button"
            onClick={onOpenChat}
            className="h-11 rounded-xl bg-zinc-900 px-6 text-sm font-medium text-white hover:bg-zinc-800"
          >
            Чат по инсайту
          </button>
          <FeedbackButtons insightId={insight.id} />
          <RoleSwitch />
        </div>
      </div>
    </header>
  );
}
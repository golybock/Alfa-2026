import Link from "next/link";
import { formatDate } from "../../lib/dates";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { FeedItemDto, Role } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ImpactBadge from "../ui/ImpactBadge";
import KindBadge from "../ui/KindBadge";
import PendingAnalysisNote from "./PendingAnalysisNote";

type InsightRowProps = { item: FeedItemDto; companyId: string; role: Role };

export default function InsightRow({ item, companyId, role }: InsightRowProps) {
  const significance = item.relevance.significance ?? null;
  const direction = item.direction ?? null;
  const topic = item.relevance.reasons[0]?.label ?? null;
  const insightHeadline = item.insight_headline ?? null;

  return (
    <li className="flex items-stretch justify-between gap-6 rounded-2xl bg-white px-5 py-4">
      <div className="min-w-0">
        {direction || significance || topic ? (
          <div className="flex flex-wrap items-center gap-2">
            {direction ? <KindBadge kind={direction} /> : null}
            {significance ? <ImpactBadge significance={significance} /> : null}
            {topic ? <p className="text-xs text-zinc-500">{topic}</p> : null}
          </div>
        ) : null}
        <h2 className="mt-3 line-clamp-2 text-base font-semibold">{insightHeadline ?? item.headline}</h2>
        <p className="mt-1 line-clamp-1 text-sm text-zinc-600">{item.summary}</p>
        {item.relevance.reasons.length > 0 ? (
          <p className="mt-2 text-xs text-zinc-500">
            Почему вы:{" "}
            <em className="not-italic text-zinc-700">
              {item.relevance.reasons.map((reason) => reason.text).join(" ")}
            </em>
          </p>
        ) : null}
      </div>
      <div className="flex shrink-0 flex-col items-end justify-between gap-3">
        <p className="flex max-w-96 items-center gap-1 text-xs text-zinc-500">
          <a
            href={item.source.url}
            target="_blank"
            rel="noreferrer"
            className="flex shrink-0 items-center gap-1 text-blue-600 hover:underline"
          >
            {item.source.name}
            <Icon name="arrowUpRight" className="h-3.5 w-3.5" />
          </a>
          {insightHeadline ? <em className="truncate not-italic">· {item.headline}</em> : null}
          ·
          <time dateTime={item.source.published_at} className="shrink-0">
            {formatDate(item.source.published_at)}
          </time>
        </p>
        {item.analysis_status === "ready" ? (
          <Link
            href={`${ANALYTICS_HREF}/${item.event_id}`}
            className="flex h-11 items-center rounded-xl border border-zinc-200 px-6 text-sm font-medium transition-colors hover:bg-zinc-50"
          >
            Открыть
          </Link>
        ) : (
          <PendingAnalysisNote item={item} companyId={companyId} role={role} />
        )}
      </div>
    </li>
  );
}
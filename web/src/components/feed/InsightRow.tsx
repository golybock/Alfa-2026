import Link from "next/link";
import { formatDate } from "../../lib/dates";
import { pendingAnalysisLabels } from "../../lib/labels";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { FeedItemDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import RankBadge from "../ui/RankBadge";

type InsightRowProps = { item: FeedItemDto };

export default function InsightRow({ item }: InsightRowProps) {
  return (
    <li className="flex items-stretch justify-between gap-6 rounded-2xl bg-white px-5 py-4">
      <div className="min-w-0">
        <RankBadge rank={item.relevance.rank} />
        <h2 className="mt-3 text-base font-semibold">{item.headline}</h2>
        <p className="mt-1 text-sm text-zinc-600">{item.summary}</p>
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
        <p className="flex items-center gap-1 text-xs text-zinc-500">
          <a
            href={item.source.url}
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-1 text-blue-600 hover:underline"
          >
            {item.source.name}
            <Icon name="arrowUpRight" className="h-3.5 w-3.5" />
          </a>
          ·
          <time dateTime={item.source.published_at}>{formatDate(item.source.published_at)}</time>
        </p>
        {item.analysis_status === "ready" ? (
          <Link
            href={`${ANALYTICS_HREF}/${item.event_id}`}
            className="flex h-11 items-center rounded-xl border border-zinc-200 px-6 text-sm font-medium transition-colors hover:bg-zinc-50"
          >
            Открыть
          </Link>
        ) : (
          <p
            className={`max-w-48 text-right text-xs ${item.analysis_status === "failed" ? "text-alfa" : "text-zinc-500"}`}
          >
            {pendingAnalysisLabels[item.analysis_status]}
          </p>
        )}
      </div>
    </li>
  );
}
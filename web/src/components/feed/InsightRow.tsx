import { formatDate } from "../../lib/dates";
import type { FeedItemDto, Role } from "../../lib/types/api";
import ImpactBadge from "../ui/ImpactBadge";
import KindBadge from "../ui/KindBadge";
import OpenInsightLink from "./OpenInsightLink";
import PendingAnalysisNote from "./PendingAnalysisNote";

type InsightRowProps = { item: FeedItemDto; companyId: string; role: Role };

export default function InsightRow({ item, companyId, role }: InsightRowProps) {
  const significance = item.relevance.significance ?? null;
  const direction = item.direction ?? null;
  const insightHeadline = item.insight_headline ?? null;

  return (
    <li className="flex items-stretch justify-between gap-6 rounded-2xl bg-white px-5 py-4 transition-shadow duration-150 hover:shadow-md">
      <div className="min-w-0">
        {direction || significance ? (
          <div className="flex flex-wrap items-center gap-2">
            {direction ? <KindBadge kind={direction} /> : null}
            {significance ? <ImpactBadge significance={significance} /> : null}
          </div>
        ) : null}
        <h2 className="mt-3 line-clamp-2 text-base font-semibold">{insightHeadline ?? item.headline}</h2>
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
            className="shrink-0 hover:text-zinc-900 hover:underline"
          >
            {item.source.name}
          </a>
          {insightHeadline ? <em className="truncate not-italic">· {item.headline}</em> : null}
          ·
          <time dateTime={item.source.published_at} className="shrink-0">
            {formatDate(item.source.published_at)}
          </time>
        </p>
        {item.analysis_status === "ready" ? (
          <OpenInsightLink companyId={companyId} eventId={item.event_id} role={role} />
        ) : (
          <PendingAnalysisNote item={item} companyId={companyId} role={role} />
        )}
      </div>
    </li>
  );
}
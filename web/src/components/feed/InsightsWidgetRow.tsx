import Link from "next/link";
import { pendingAnalysisLabels } from "../../lib/labels";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { FeedItemDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import ImpactBadge from "../ui/ImpactBadge";
import KindBadge from "../ui/KindBadge";

type InsightsWidgetRowProps = { item: FeedItemDto };

export default function InsightsWidgetRow({ item }: InsightsWidgetRowProps) {
  const href = item.analysis_status === "ready" ? `${ANALYTICS_HREF}/${item.event_id}` : ANALYTICS_HREF;
  const note =
    item.analysis_status === "ready"
      ? item.relevance.reasons.map((reason) => reason.text).join(" ")
      : pendingAnalysisLabels[item.analysis_status];
  const significance = item.relevance.significance ?? null;
  const direction = item.direction ?? null;

  return (
    <li>
      <Link
        href={href}
        className="flex items-center gap-3 rounded-xl bg-zinc-100 px-4 py-3 transition-colors hover:bg-zinc-200/70"
      >
        {direction ? <KindBadge kind={direction} /> : null}
        {significance ? <ImpactBadge significance={significance} /> : null}
        <p className="line-clamp-2 min-w-0 flex-1 text-sm font-medium">{item.insight_headline ?? item.headline}</p>
        <p className="line-clamp-1 w-64 shrink-0 text-xs text-zinc-500">{note}</p>
        <Icon name="send" className="h-4 w-4 shrink-0" />
      </Link>
    </li>
  );
}
"use client";

import { useState } from "react";
import type { FeedItemDto, Role } from "../../lib/types/api";
import InsightProgress from "./InsightProgress";
import InsightReadyNotice from "./InsightReadyNotice";
import OpenInsightLink from "./OpenInsightLink";
import PendingAnalysisNote from "./PendingAnalysisNote";

type InsightStatusPanelProps = { item: FeedItemDto; companyId: string; role: Role };

export default function InsightStatusPanel({ item, companyId, role }: InsightStatusPanelProps) {
  const pending = item.analysis_status === "queued" || item.analysis_status === "processing";
  const [seenPending, setSeenPending] = useState(pending);
  if (pending && !seenPending) {
    setSeenPending(true);
  }

  if (pending) {
    return <InsightProgress />;
  }

  if (item.analysis_status === "ready") {
    return (
      <div className="flex items-center gap-3">
        {seenPending ? <InsightReadyNotice /> : null}
        <OpenInsightLink companyId={companyId} eventId={item.event_id} role={role} />
      </div>
    );
  }

  return <PendingAnalysisNote item={item} companyId={companyId} role={role} />;
}
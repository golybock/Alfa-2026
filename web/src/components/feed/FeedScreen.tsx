"use client";

import { useActiveContext } from "../../lib/hooks/session";
import InsightsFeed from "./InsightsFeed";

export default function FeedScreen() {
  const active = useActiveContext();
  if (!active) {
    return null;
  }

  return (
    <InsightsFeed
      key={`${active.company.company_id}-${active.context.role}`}
      companyId={active.company.company_id}
      companyName={active.company.name}
      role={active.context.role}
    />
  );
}

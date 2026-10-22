"use client";

import { use } from "react";
import { useActiveContext } from "../../lib/hooks/session";
import InsightCardContent from "./InsightCardContent";

type InsightCardScreenProps = { params: Promise<{ eventId: string }> };

export default function InsightCardScreen({ params }: InsightCardScreenProps) {
  const { eventId } = use(params);
  const active = useActiveContext();
  if (!active) {
    return null;
  }

  return (
    <InsightCardContent companyId={active.company.company_id} eventId={eventId} role={active.context.role} />
  );
}

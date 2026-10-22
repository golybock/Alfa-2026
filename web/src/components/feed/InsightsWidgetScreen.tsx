"use client";

import { useActiveContext } from "../../lib/hooks/session";
import InsightsWidget from "./InsightsWidget";

export default function InsightsWidgetScreen() {
  const active = useActiveContext();
  if (!active) {
    return null;
  }

  return <InsightsWidget companyId={active.company.company_id} role={active.context.role} />;
}

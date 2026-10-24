"use client";

import Link from "next/link";
import { usePrefetchInsight } from "../../lib/hooks/queries";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { Role } from "../../lib/types/api";

type OpenInsightLinkProps = { companyId: string; eventId: string; role: Role };

export default function OpenInsightLink({ companyId, eventId, role }: OpenInsightLinkProps) {
  const prefetch = usePrefetchInsight(companyId, eventId, role);

  return (
    <Link
      href={`${ANALYTICS_HREF}/${eventId}`}
      onPointerEnter={prefetch}
      onFocus={prefetch}
      className="flex h-11 items-center rounded-xl border border-zinc-200 px-6 text-sm font-medium hover:bg-zinc-50"
    >
      Открыть
    </Link>
  );
}
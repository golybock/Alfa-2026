"use client";

import Link from "next/link";
import { useState } from "react";
import { formatDateTime } from "../../lib/dates";
import { useActiveContext } from "../../lib/hooks/session";
import { roleLabels } from "../../lib/labels";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { InsightDto } from "../../lib/types/api";
import Drawer from "../ui/Drawer";
import Icon from "../ui/Icon";
import RankBadge from "../ui/RankBadge";
import RoleSwitch from "../ui/RoleSwitch";
import ChatPanel from "./ChatPanel";
import FeedbackButtons from "./FeedbackButtons";
import WhyPanel from "./WhyPanel";

type Panel = "why" | "chat" | null;

type InsightCardHeaderProps = { insight: InsightDto };

export default function InsightCardHeader({ insight }: InsightCardHeaderProps) {
  const [panel, setPanel] = useState<Panel>(null);
  const companyName = useActiveContext()?.company.name ?? "вашей компании";
  const close = () => setPanel(null);

  return (
    <header className="rounded-2xl bg-white p-5">
      <div className="flex items-start justify-between gap-6">
        <div>
          <div className="flex items-center gap-2">
            <Link href={ANALYTICS_HREF} className="flex items-center gap-1 text-xs text-zinc-600 hover:text-zinc-900">
              <Icon name="chevronLeft" className="h-3.5 w-3.5" />
              Все инсайты
            </Link>
            <RankBadge rank={insight.relevance.rank} />
          </div>
          <h1 className="mt-3 text-2xl font-semibold">{insight.headline}</h1>
          <p className="mt-2 text-sm text-zinc-500">
            Анализ на <time dateTime={insight.as_of}>{formatDateTime(insight.as_of)}</time> · акцент для роли «
            {roleLabels[insight.role]}»
          </p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-3">
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setPanel("why")}
              className="h-10 rounded-xl bg-zinc-100 px-5 text-sm font-medium hover:bg-zinc-200"
            >
              Почему я это вижу
            </button>
            <button
              type="button"
              onClick={() => setPanel("chat")}
              className="h-10 rounded-xl bg-zinc-900 px-5 text-sm font-medium text-white hover:bg-zinc-800"
            >
              Чат по инсайту
            </button>
          </div>
          <FeedbackButtons insightId={insight.id} />
          <RoleSwitch />
        </div>
      </div>
      {panel === "why" ? (
        <Drawer
          title="Почему я это вижу"
          subtitle={`Как система выбрала этот инсайт для ${companyName}`}
          onClose={close}
        >
          <WhyPanel insight={insight} />
        </Drawer>
      ) : null}
      {panel === "chat" ? (
        <Drawer title="Чат по инсайту" subtitle="Отвечаю только по этому инсайту и данным вашей компании" onClose={close}>
          <ChatPanel insight={insight} />
        </Drawer>
      ) : null}
    </header>
  );
}

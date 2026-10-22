"use client";

import Link from "next/link";
import { ApiRequestError, describeError } from "../../lib/api/client";
import { useInsight } from "../../lib/hooks/queries";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { Role } from "../../lib/types/api";
import InsightCardBody from "./InsightCardBody";
import EvidenceViewerProvider from "./EvidenceViewerProvider";
import InsightCardHeader from "./InsightCardHeader";

const notPreparedCodes = ["insight_not_prepared", "not_found"];

function failureText(error: Error): string {
  if (error instanceof ApiRequestError && notPreparedCodes.includes(error.error.code)) {
    return "Полный анализ этого события ещё не подготовлен. Короткая строка и первоисточник доступны в ленте.";
  }
  return `Не удалось загрузить карточку: ${describeError(error)}`;
}

type InsightCardContentProps = { companyId: string; eventId: string; role: Role };

export default function InsightCardContent({ companyId, eventId, role }: InsightCardContentProps) {
  const insight = useInsight(companyId, eventId, role);

  if (insight.isPending) {
    return <p className="rounded-2xl bg-white p-5 text-sm text-zinc-500">Загружаем карточку…</p>;
  }

  if (insight.isError || insight.data.state === "waiting") {
    return (
      <div role={insight.isError ? "alert" : "status"} className="rounded-2xl bg-white p-5 text-sm">
        <p>
          {insight.isError
            ? failureText(insight.error)
            : "Полная карточка готовится. Пока доступны короткая строка и первоисточник в ленте."}
        </p>
        <Link href={ANALYTICS_HREF} className="mt-3 inline-block text-blue-600 hover:underline">
          Вернуться к ленте
        </Link>
      </div>
    );
  }

  return (
    <EvidenceViewerProvider limitations={insight.data.data.limitations}>
      <InsightCardHeader insight={insight.data.data} />
      <InsightCardBody insight={insight.data.data} />
    </EvidenceViewerProvider>
  );
}

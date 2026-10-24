"use client";

import { evidenceKinds } from "../../lib/claimKinds";
import { formatDate, formatDayMonthNumeric } from "../../lib/dates";
import { contributionLabel, criticStatusLabels, roleEmphasis, roleLabels } from "../../lib/labels";
import type { InsightDto } from "../../lib/types/api";
import ClaimBadge from "../ui/ClaimBadge";
import EvidenceRefButton from "../ui/EvidenceRefButton";
import Icon from "../ui/Icon";
import SystemSteps from "./SystemSteps";

const numberFormat = new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const headingClass = "mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500";

type WhyPanelProps = { insight: InsightDto };

export default function WhyPanel({ insight }: WhyPanelProps) {
  const { relevance, verification } = insight;
  const baseScore = relevance.contributions.filter((item) => item.known).reduce((sum, item) => sum + item.contribution, 0);
  const newsDate = Object.values(insight.evidence).find((item) => item.type === "news" && item.date)?.date ?? null;

  return (
    <div className="flex flex-col gap-6">
      <section>
        <h3 className={headingClass}>Из чего сложился балл</h3>
        <ul className="flex flex-col gap-3">
          {relevance.contributions.map((item) => (
            <li key={item.code}>
              <p className="flex items-baseline justify-between gap-4 text-sm">
                {contributionLabel(item.code)}
                <strong className="shrink-0 text-xs font-medium">
                  {item.known ? `${numberFormat.format(item.contribution)} из ${numberFormat.format(item.weight)}` : "нет данных"}
                </strong>
              </p>
              <div className="mt-1.5 h-1 rounded-full bg-zinc-100">
                <div
                  className="h-1 rounded-full bg-alfa"
                  style={{ width: `${item.known && item.weight > 0 ? Math.min(100, (item.contribution / item.weight) * 100) : 0}%` }}
                />
              </div>
            </li>
          ))}
        </ul>
        <p className="mt-4 text-sm">
          <strong className="font-semibold">Базовый балл {numberFormat.format(baseScore)}</strong>
          <em className="ml-3 text-xs not-italic text-zinc-500">
            Итоговый score {numberFormat.format(relevance.score)}, место №{relevance.rank}
          </em>
        </p>
        <p className="mt-3 flex items-start gap-2 text-xs text-zinc-500">
          <Icon name="info" className="mt-0.5 h-3.5 w-3.5" />
          Свежесть {newsDate ? `(новость от ${formatDayMonthNumeric(newsDate)}) ` : "новости "}влияет только на порядок в ленте и в
          базовый балл не входит.
        </p>
      </section>
      {relevance.reasons.length > 0 ? (
        <section>
          <h3 className={headingClass}>Причины отбора</h3>
          <ul className="flex flex-col gap-2 text-sm">
            {relevance.reasons.map((reason) => (
              <li key={reason.code}>
                {reason.text}{" "}
                {reason.evidence_refs.map((ref) => (
                  <EvidenceRefButton key={ref} id={ref} evidence={insight.evidence} />
                ))}
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      <section className="rounded-2xl bg-zinc-100 p-4">
        <h3 className="text-sm font-semibold">Как мы подстроили под вашу роль</h3>
        <p className="mt-1 text-xs text-zinc-600">
          {roleLabels[insight.role]}. Роль не влияет на выбор инсайта, она меняет формулировки, вопросы и действия:{" "}
          {roleEmphasis[insight.role]}.
        </p>
      </section>
      <section>
        <h3 className={headingClass}>Источники карточки</h3>
        <ul className="flex flex-col gap-3">
          {Object.entries(insight.evidence).map(([id, item]) => (
            <li key={id} className="grid grid-cols-[150px_1fr_auto] items-start gap-3 text-xs">
              <ClaimBadge kind={evidenceKinds[item.type]} />
              <p className="text-zinc-800">
                {item.source}
                {item.date ? ` · ${formatDate(item.date)}` : ""}
                {item.page ? ` · стр. ${item.page}` : ""}
              </p>
              <div className="flex items-center gap-2">
                <EvidenceRefButton id={id} evidence={insight.evidence} />
                {item.url || item.page_image_url ? (
                  <a
                    href={item.url || item.page_image_url || undefined}
                    target="_blank"
                    rel="noreferrer"
                    aria-label={`Открыть источник ${id}`}
                    className="text-blue-600"
                  >
                    <Icon name="arrowUpRight" className="h-4 w-4" />
                  </a>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      </section>
      <section>
        <h3 className={headingClass}>Шаги системы</h3>
        <SystemSteps insightId={insight.id} />
      </section>
      {insight.limitations.length > 0 ? (
        <section className="rounded-2xl bg-zinc-100 p-4">
          <h3 className="text-sm font-semibold">Чего мы о вас не знаем</h3>
          <ul className="mt-1 flex flex-col gap-1 text-xs text-zinc-500">
            {insight.limitations.map((limitation) => (
              <li key={limitation}>{limitation}</li>
            ))}
          </ul>
        </section>
      ) : null}
      <section>
        <h3 className={headingClass}>Проверки</h3>
        <ul className="flex flex-col gap-2 text-xs">
          <li>{verification.code_status === "passed" ? "Проверки кодом пройдены" : "Проверки кодом не пройдены"}</li>
          <li>
            {criticStatusLabels[verification.critic.status]}
            {verification.critic.reason ? `: ${verification.critic.reason}` : ""}
          </li>
          <li>Починок после проверок: {verification.repair_count}</li>
          <li className="text-zinc-500">
            Трасса запуска: <code>{insight.trace_id}</code>
          </li>
        </ul>
      </section>
    </div>
  );
}
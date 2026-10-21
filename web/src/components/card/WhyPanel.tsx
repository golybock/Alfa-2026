import { formatDate } from "../../lib/dates";
import { contributionLabel, criticStatusLabels, evidenceTypeLabels, reviewStatusLabels } from "../../lib/labels";
import type { InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";

const numberFormat = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 2 });

type WhyPanelProps = { insight: InsightDto };

export default function WhyPanel({ insight }: WhyPanelProps) {
  const { relevance, verification } = insight;

  return (
    <div className="flex flex-col gap-6">
      <section>
        <h3 className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Причины отбора</h3>
        <ul className="flex flex-col gap-2 text-sm">
          {relevance.reasons.map((reason) => (
            <li key={reason.code}>
              {reason.text}{" "}
              {reason.evidence_refs.map((ref) => (
                <code key={ref} className="rounded bg-zinc-100 px-1.5 text-[11px] text-zinc-500">
                  {ref}
                </code>
              ))}
            </li>
          ))}
        </ul>
      </section>
      <section>
        <h3 className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Вклад признаков в score</h3>
        <table className="w-full text-left text-xs">
          <thead className="text-zinc-500">
            <tr>
              <th className="pb-2 font-normal">Признак</th>
              <th className="pb-2 font-normal">Значение</th>
              <th className="pb-2 font-normal">Вес</th>
              <th className="pb-2 font-normal">Вклад</th>
            </tr>
          </thead>
          <tbody>
            {relevance.contributions.map((item) => (
              <tr key={item.code} className="border-t border-zinc-100">
                <td className="py-2 pr-2">{contributionLabel(item.code)}</td>
                <td className="py-2">{item.known ? numberFormat.format(item.value) : "нет данных"}</td>
                <td className="py-2">{numberFormat.format(item.weight)}</td>
                <td className="py-2">{numberFormat.format(item.contribution)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="mt-3 text-xs text-zinc-500">
          Score {numberFormat.format(relevance.score)}, место №{relevance.rank}. Это приоритет в ленте, а не процент
          влияния на бизнес.
        </p>
      </section>
      <section>
        <h3 className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Доказательства</h3>
        <ul className="flex flex-col gap-3">
          {Object.entries(insight.evidence).map(([id, item]) => (
            <li key={id} className="grid grid-cols-[32px_1fr_16px] items-start gap-2 text-xs">
              <code className="font-medium">{id}</code>
              <div>
                <p className="font-medium">
                  {evidenceTypeLabels[item.type]} · {item.source}
                </p>
                {item.quote ? <p className="mt-1 text-zinc-600">«{item.quote}»</p> : null}
                {item.key ? (
                  <p className="mt-1 text-zinc-600">
                    {item.key}: {JSON.stringify(item.value)}
                  </p>
                ) : null}
                <p className="mt-1 text-zinc-500">
                  {item.date ? formatDate(item.date) : "Дата не указана"}
                  {item.page ? ` · стр. ${item.page}` : ""}
                  {item.review_status ? ` · ${reviewStatusLabels[item.review_status]}` : ""}
                  {item.approximate ? " · по графику, приблизительно" : ""}
                </p>
              </div>
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
            </li>
          ))}
        </ul>
      </section>
      <section>
        <h3 className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Проверки</h3>
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

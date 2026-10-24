import { formatDate, formatDateNumeric } from "../../lib/dates";
import { criticStatusLabels, reviewStatusLabels, roleLabels } from "../../lib/labels";
import type { ClaimDto, EvidenceDto, EvidenceType, InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import KindBadge from "../ui/KindBadge";
import SourceLinkRow from "../ui/SourceLinkRow";
import ActionList from "./ActionList";
import CardBlock from "./CardBlock";
import ClaimList from "./ClaimList";
import ManagerContactButton from "./ManagerContactButton";

type EvidenceEntry = { id: string; item: EvidenceDto };

function evidenceOf(
  claimIds: ReadonlyArray<string>,
  claims: ReadonlyMap<string, ClaimDto>,
  evidence: Readonly<Record<string, EvidenceDto>>,
  type: EvidenceType,
): EvidenceEntry[] {
  const refs = new Set(claimIds.flatMap((id) => claims.get(id)?.refs ?? []));
  return [...refs]
    .map((id) => ({ id, item: evidence[id] }))
    .filter((entry): entry is EvidenceEntry => entry.item !== undefined && entry.item.type === type);
}

// Агент цитирует одну статью несколькими доказательствами; в списке источников статья показывается одной строкой.
type SourceGroup = { ids: string[]; source: string; url: string };

function groupByUrl(entries: EvidenceEntry[]): SourceGroup[] {
  const groups = new Map<string, SourceGroup>();
  for (const { id, item } of entries) {
    if (!item.url) continue;
    const group = groups.get(item.url) ?? { ids: [], source: item.source, url: item.url };
    group.ids.push(id);
    groups.set(item.url, group);
  }
  return [...groups.values()];
}

// E1, E6, E7, E8 → «E1, E6–E8».
function idRanges(ids: string[]): string {
  const numbers = ids.map((id) => Number(id.slice(1))).sort((a, b) => a - b);
  const parts: string[] = [];
  for (let start = 0; start < numbers.length; ) {
    let end = start;
    while (end + 1 < numbers.length && numbers[end + 1] === numbers[end] + 1) end++;
    parts.push(end > start ? `E${numbers[start]}–E${numbers[end]}` : `E${numbers[start]}`);
    start = end + 1;
  }
  return parts.join(", ");
}

type InsightCardBodyProps = { insight: InsightDto; onOpenWhy: () => void };

export default function InsightCardBody({ insight, onOpenWhy }: InsightCardBodyProps) {
  const claims = new Map(insight.claims.map((claim) => [claim.id, claim]));
  const news = evidenceOf(insight.what_happened, claims, insight.evidence, "news");
  const bankSources = evidenceOf(insight.bank_view.claim_ids, claims, insight.evidence, "bank_research");
  const bankFound = insight.bank_view.status === "found";
  const features = insight.company_traits
    ? insight.company_traits.map((trait) => trait.label)
    : insight.relevance.reasons.map((reason) => reason.label).filter((label): label is string => Boolean(label));
  const bankDate =
    insight.bank_data_as_of ??
    Object.values(insight.evidence)
      .filter((item) => item.type === "bank_research" && item.date)
      .map((item) => item.date as string)
      .sort()
      .at(-1) ??
    null;
  const codePassed = insight.verification.code_status === "passed";
  const criticPassed = insight.verification.critic.status === "passed";

  return (
    <div className="mt-4 flex flex-col gap-4">
      <div className="grid grid-cols-3 gap-4">
        <CardBlock title="Что произошло">
          <ClaimList ids={insight.what_happened} claims={claims} evidence={insight.evidence} stacked />
          {news.length === 0 ? (
            <p className="mt-3 text-xs text-zinc-500">Ссылка на источник: данных нет</p>
          ) : (
            groupByUrl(news).map((group) => (
              <SourceLinkRow key={group.url} title={`${idRanges(group.ids)} · ${group.source}`} url={group.url} />
            ))
          )}
        </CardBlock>
        <CardBlock title="Что это меняет">
          {insight.consequences.length === 0 ? (
            <p className="text-sm text-zinc-500">Данных нет</p>
          ) : (
            <ul className="flex flex-col gap-4">
              {insight.consequences.map((consequence) => (
                <li key={`${consequence.type}-${consequence.claim_ids.join("-")}`}>
                  <ClaimList
                    ids={consequence.claim_ids}
                    claims={claims}
                    evidence={insight.evidence}
                    stacked
                    leading={<KindBadge kind={consequence.type} />}
                  />
                </li>
              ))}
            </ul>
          )}
        </CardBlock>
        <CardBlock title="Что стоит предпринять" subtitle={`Подобрано для роли: ${roleLabels[insight.role].toLowerCase()}`}>
          {insight.actions.length === 0 ? (
            <p className="text-sm text-zinc-500">Данных нет</p>
          ) : (
            <>
              <ActionList ids={insight.actions} claims={claims} evidence={insight.evidence} />
              <ManagerContactButton insight={insight} />
            </>
          )}
        </CardBlock>
      </div>
      <section aria-label="Почему это касается вас" className="flex items-start gap-8 rounded-2xl bg-white p-5">
        <div className="w-48 shrink-0">
          <h2 className="text-base font-semibold">Почему это касается вас</h2>
          <button
            type="button"
            onClick={onOpenWhy}
            className="mt-3 h-11 rounded-xl bg-zinc-900 px-5 text-sm font-medium text-white hover:bg-zinc-800"
          >
            Почему я это вижу
          </button>
        </div>
        <div className="min-w-0 flex-1">
          {insight.why_you ? <p className="mb-3 text-sm font-medium text-zinc-900">{insight.why_you}</p> : null}
          <ClaimList ids={insight.why_it_matters} claims={claims} evidence={insight.evidence} />
          {features.length > 0 ? (
            <ul className="mt-3 flex flex-wrap gap-2">
              {features.map((feature) => (
                <li key={feature} className="rounded-lg bg-zinc-100 px-3 py-1 text-xs font-medium text-zinc-900">
                  {feature}
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </section>
      <div className="grid grid-cols-3 gap-4">
        <CardBlock title="Что говорит Альфа-Банк">
          {bankFound ? (
            <div className="rounded-xl bg-red-50 p-4">
              <ClaimList ids={insight.bank_view.claim_ids} claims={claims} evidence={insight.evidence} stacked />
              {bankSources.map(({ id, item }) => (
                <div key={id} className="mt-3 text-xs text-zinc-500">
                  <p>
                    {id} · {item.source}
                    {item.date ? ` · ${formatDate(item.date)}` : ""}
                    {item.page ? ` · стр. ${item.page}` : ""}
                  </p>
                  {item.review_status && item.review_status !== "not_reviewed" ? <p>{reviewStatusLabels[item.review_status]}</p> : null}
                  {item.page_image_url ? <SourceLinkRow title="Открыть страницу материала" url={item.page_image_url} /> : null}
                </div>
              ))}
            </div>
          ) : (
            <p className="rounded-xl bg-zinc-100 p-4 text-sm text-zinc-600">Позиции банка по этой теме нет.</p>
          )}
        </CardBlock>
        <CardBlock title="Что мы о вас не знаем">
          {insight.limitations.length === 0 ? (
            <p className="text-sm text-zinc-500">Данных нет</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {insight.limitations.map((limitation) => (
                <li key={limitation} className="flex items-start gap-2 text-sm text-zinc-600">
                  <Icon name="info" className="mt-0.5 h-4 w-4 shrink-0" />
                  {limitation}
                </li>
              ))}
            </ul>
          )}
        </CardBlock>
        <CardBlock title="На чём основан вывод">
          <ul className="flex flex-col gap-2 text-sm">
            <li className="flex items-start gap-2">
              <Icon name="check" className={`mt-0.5 h-4 w-4 shrink-0 ${codePassed ? "text-green-600" : "text-alfa"}`} />
              {codePassed ? "Расчёт проверен кодом" : "Проверки кодом не пройдены"}
            </li>
            <li className="flex items-start gap-2">
              <Icon name={criticPassed ? "check" : "info"} className={`mt-0.5 h-4 w-4 shrink-0 ${criticPassed ? "text-green-600" : "text-orange-600"}`} />
              {criticStatusLabels[insight.verification.critic.status]}
              {insight.verification.critic.reason ? ` (${insight.verification.critic.reason})` : ""}
            </li>
            <li className="flex items-start gap-2">
              <Icon name="info" className="mt-0.5 h-4 w-4 shrink-0 text-zinc-500" />
              {bankDate ? `В основе лежат данные банка на момент ${formatDateNumeric(bankDate)}` : "Данные банка: данных нет"}
            </li>
          </ul>
          <p className="mt-3 text-xs text-zinc-500">
            Аналитика не является инвестиционной рекомендацией. Вывод ИИ не является позицией Альфа-Банка.
          </p>
        </CardBlock>
      </div>
    </div>
  );
}
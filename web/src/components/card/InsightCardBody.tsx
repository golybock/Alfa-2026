import { formatDate } from "../../lib/dates";
import { criticStatusLabels, reviewStatusLabels } from "../../lib/labels";
import type { ClaimDto, EvidenceDto, EvidenceType, InsightDto } from "../../lib/types/api";
import Icon from "../ui/Icon";
import KindBadge from "../ui/KindBadge";
import RankBadge from "../ui/RankBadge";
import SourceLinkRow from "../ui/SourceLinkRow";
import CardBlock from "./CardBlock";
import ClaimList from "./ClaimList";

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

type InsightCardBodyProps = { insight: InsightDto };

export default function InsightCardBody({ insight }: InsightCardBodyProps) {
  const claims = new Map(insight.claims.map((claim) => [claim.id, claim]));
  const news = evidenceOf(insight.what_happened, claims, insight.evidence, "news");
  const bankSources = evidenceOf(insight.bank_view.claim_ids, claims, insight.evidence, "bank_research");
  const bankFound = insight.bank_view.status === "found";
  const blocks = ["what", "why", "consequences", "actions", "bank", "shown"];
  const numberOf = (block: string) => blocks.indexOf(block) + 1;

  return (
    <div className="mt-4 flex flex-col gap-4">
      <div className="grid grid-cols-[1fr_340px] items-start gap-4">
        <div className="flex flex-col gap-4">
          <CardBlock number={numberOf("what")} title="Что произошло">
            <ClaimList ids={insight.what_happened} claims={claims} evidence={insight.evidence} />
			  {news.length === 0 ? (
				  <p className="mt-3 text-xs text-zinc-500">Ссылка на источник: данных нет</p>
			  ) : (
				  groupByUrl(news).map((group) => (
					  <SourceLinkRow key={group.url} title={`${idRanges(group.ids)} · ${group.source}`} url={group.url} />
				  ))
			  )}
          </CardBlock>
          <CardBlock number={numberOf("why")} title="Почему это важно именно вам">
            <ClaimList ids={insight.why_it_matters} claims={claims} evidence={insight.evidence} />
          </CardBlock>
          <CardBlock number={numberOf("consequences")} title="Какие последствия возможны">
            {insight.consequences.length === 0 ? (
              <p className="text-sm text-zinc-500">Данных нет</p>
            ) : (
              <ul className="flex flex-col gap-4">
                {insight.consequences.map((consequence) => (
                  <li key={`${consequence.type}-${consequence.claim_ids.join("-")}`} className="flex flex-col gap-2">
                    <KindBadge kind={consequence.type} />
                    <ClaimList ids={consequence.claim_ids} claims={claims} evidence={insight.evidence} />
                  </li>
                ))}
              </ul>
            )}
          </CardBlock>
        </div>
        <div className="flex flex-col gap-4">
          <CardBlock number={numberOf("actions")} title="Что стоит проверить">
            {insight.actions.length === 0 ? (
              <p className="text-sm text-zinc-500">Данных нет</p>
            ) : (
              <>
                <ClaimList ids={insight.actions} claims={claims} evidence={insight.evidence} />
                <button
                  type="button"
                  disabled
                  title="Пока недоступно"
                  className="mt-4 h-10 cursor-not-allowed rounded-xl bg-zinc-100 px-4 text-sm font-medium opacity-50"
                >
                  Связаться с менеджером
                </button>
              </>
            )}
          </CardBlock>
          <CardBlock number={numberOf("bank")} title="Как это оценивает банк">
            {bankFound ? (
              <div className="rounded-xl bg-red-50 p-4">
                <ClaimList ids={insight.bank_view.claim_ids} claims={claims} evidence={insight.evidence} />
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
              <p className="rounded-xl bg-zinc-100 p-4 text-sm text-zinc-600">
                Позиции банка по этой теме нет.
              </p>
            )}
          </CardBlock>
          <CardBlock number={numberOf("shown")} title="Почему показано">
            <RankBadge rank={insight.relevance.rank} />
            {insight.relevance.reasons.length === 0 ? (
              <p className="mt-3 text-sm text-zinc-500">Данных нет</p>
            ) : (
              <ul className="mt-3 flex flex-col gap-2 text-sm">
                {insight.relevance.reasons.map((reason) => (
                  <li key={reason.code}>{reason.text}</li>
                ))}
              </ul>
            )}
            <p className="mt-3 flex items-start gap-2 text-xs text-zinc-500">
              <Icon name="info" className="h-4 w-4 shrink-0" />
              {insight.verification.code_status === "passed" ? "Проверки кодом пройдены. " : "Проверки кодом не пройдены. "}
              {criticStatusLabels[insight.verification.critic.status]}
              {insight.verification.critic.reason ? ` (${insight.verification.critic.reason})` : ""}
            </p>
          </CardBlock>
          <section className="rounded-2xl bg-zinc-100 p-5">
            <h2 className="text-sm font-semibold">Чего мы о вас не знаем</h2>
            {insight.limitations.length === 0 ? (
              <p className="mt-2 text-xs text-zinc-600">Данных нет</p>
            ) : (
              <ul className="mt-2 flex list-disc flex-col gap-1 pl-4 text-xs leading-5 text-zinc-600">
                {insight.limitations.map((limitation) => (
                  <li key={limitation}>{limitation}</li>
                ))}
              </ul>
            )}
          </section>
        </div>
      </div>
      <footer className="flex flex-col gap-1 text-xs text-zinc-500">
        <p>Информация не является индивидуальной инвестиционной рекомендацией.</p>
        {bankFound ? null : <p>Вывод сделан моделью по внешним источникам и не является позицией Альфа-Банка.</p>}
      </footer>
    </div>
  );
}

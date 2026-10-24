import Image from "next/image";
import { evidenceKinds } from "../../lib/claimKinds";
import { formatDate } from "../../lib/dates";
import { evidenceTypeLabels, reviewStatusLabels } from "../../lib/labels";
import type { EvidenceDto, JsonValue } from "../../lib/types/api";
import ClaimBadge from "../ui/ClaimBadge";
import SourceLinkRow from "../ui/SourceLinkRow";

function formatValue(value: JsonValue | null): string {
  if (value === null) {
    return "нет данных";
  }
  return typeof value === "string" ? value : JSON.stringify(value);
}

type EvidenceDetailProps = { id: string; evidence: EvidenceDto; limitations: ReadonlyArray<string> };

export default function EvidenceDetail({ id, evidence, limitations }: EvidenceDetailProps) {
  const kind = evidenceKinds[evidence.type];
  const pageImage = evidence.page_image_url;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-2">
        <ClaimBadge kind={kind} />
        <h3 className="text-base font-semibold">{evidence.source}</h3>
        <p className="text-xs text-zinc-500">
          <code>{id}</code> · {evidenceTypeLabels[evidence.type]} ·{" "}
          {evidence.date ? <time dateTime={evidence.date}>{formatDate(evidence.date)}</time> : "дата не указана"}
        </p>
      </div>
      {evidence.quote ? (
        <section>
          <h4 className="mb-2 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Цитата</h4>
          <blockquote className="rounded-2xl bg-zinc-100 p-4 text-sm leading-6">«{evidence.quote}»</blockquote>
        </section>
      ) : null}
      {kind === "company_data" ? (
        <section>
          <h4 className="mb-2 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Данные</h4>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-2xl bg-zinc-100 p-4 text-sm">
            <dt className="text-zinc-500">Признак</dt>
            <dd className="break-all font-mono">{evidence.key ?? "нет данных"}</dd>
            <dt className="text-zinc-500">Значение</dt>
            <dd className="break-all font-mono">{formatValue(evidence.value)}</dd>
            <dt className="text-zinc-500">Набор данных</dt>
            <dd>{evidence.dataset ?? "нет данных"}</dd>
          </dl>
        </section>
      ) : null}
      {evidence.type === "bank_research" ? (
        <section className="flex flex-col gap-2 text-sm">
          <h4 className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">Материал банка</h4>
          <p>{evidence.page ? `Страница ${evidence.page}` : "Страница не указана"}</p>
          {evidence.review_status && evidence.review_status !== "not_reviewed" ? <p>{reviewStatusLabels[evidence.review_status]}</p> : null}
          {evidence.approximate ? <p className="text-orange-700">Значение снято с графика, приблизительно</p> : null}
          {pageImage ? (
            <>
              <Image
                src={pageImage}
                alt={`Страница ${evidence.page ?? ""} материала «${evidence.source}»`}
                width={800}
                height={1100}
                unoptimized
                className="h-auto w-full rounded-xl border border-zinc-200"
              />
              <SourceLinkRow title="Открыть страницу" url={pageImage} />
            </>
          ) : null}
        </section>
      ) : null}
      {evidence.url ? <SourceLinkRow title="Открыть первоисточник" url={evidence.url} /> : null}
      {limitations.length > 0 ? (
        <section className="rounded-2xl bg-zinc-100 p-4">
          <h4 className="text-sm font-semibold">Ограничения карточки</h4>
          <ul className="mt-2 flex list-disc flex-col gap-1 pl-4 text-xs text-zinc-600">
            {limitations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}

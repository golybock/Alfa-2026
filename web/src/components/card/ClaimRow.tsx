import { claimKindOf } from "../../lib/claimKinds";
import { confidenceLabels, reviewStatusLabels } from "../../lib/labels";
import type { ClaimDto, EvidenceDto, ReviewStatus } from "../../lib/types/api";
import ClaimBadge from "../ui/ClaimBadge";

function unreviewedStatus(claim: ClaimDto, evidence: Readonly<Record<string, EvidenceDto>>): ReviewStatus | null {
  const statuses = claim.refs.map((ref) => evidence[ref]?.review_status ?? null);
  return statuses.find((status) => status === "not_reviewed" || status === "partially_reviewed") ?? null;
}

type ClaimRowProps = { claim: ClaimDto; evidence: Readonly<Record<string, EvidenceDto>> };

export default function ClaimRow({ claim, evidence }: ClaimRowProps) {
  const unreviewed = unreviewedStatus(claim, evidence);

  return (
    <li className="grid items-start gap-2 @md:grid-cols-[150px_1fr] @md:gap-3">
      <ClaimBadge kind={claimKindOf(claim, evidence)} />
      <div>
        <p className="text-sm leading-6">{claim.text}</p>
        <div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-zinc-500">
          {claim.refs.map((ref) => (
            <code key={ref} title={evidence[ref]?.source ?? "Доказательство не найдено"} className="rounded bg-zinc-100 px-1.5">
              {ref}
            </code>
          ))}
          {claim.origin === "ai_hypothesis" && claim.confidence ? (
            <p>Уверенность: {confidenceLabels[claim.confidence]}</p>
          ) : null}
          {claim.verification_status === "not_confirmed" ? (
            <strong className="rounded bg-amber-100 px-1.5 font-medium text-orange-700">Не подтверждено</strong>
          ) : null}
          {unreviewed ? (
            <strong className="rounded bg-zinc-200 px-1.5 font-medium text-zinc-700">{reviewStatusLabels[unreviewed]}</strong>
          ) : null}
        </div>
      </div>
    </li>
  );
}

"use client";

import type { ReactNode } from "react";
import { claimKindOf } from "../../lib/claimKinds";
import { confidenceLabels, reviewStatusLabels } from "../../lib/labels";
import type { ClaimDto, EvidenceDto, ReviewStatus } from "../../lib/types/api";
import ClaimBadge from "../ui/ClaimBadge";
import EvidenceRefButton from "../ui/EvidenceRefButton";

function unreviewedStatus(claim: ClaimDto, evidence: Readonly<Record<string, EvidenceDto>>): ReviewStatus | null {
  const statuses = claim.refs.map((ref) => evidence[ref]?.review_status ?? null);
  return statuses.find((status) => status === "partially_reviewed") ?? null;
}

type ClaimRowProps = {
  claim: ClaimDto;
  evidence: Readonly<Record<string, EvidenceDto>>;
  stacked?: boolean;
  leading?: ReactNode;
};

export default function ClaimRow({ claim, evidence, stacked = false, leading }: ClaimRowProps) {
  const unreviewed = unreviewedStatus(claim, evidence);

  return (
    <li className={stacked ? "flex flex-col gap-2" : "grid items-start gap-2 @md:grid-cols-[150px_1fr] @md:gap-3"}>
      {leading ? (
        <div className="flex flex-wrap items-center gap-2">
          {leading}
          <ClaimBadge kind={claimKindOf(claim, evidence)} />
        </div>
      ) : (
        <ClaimBadge kind={claimKindOf(claim, evidence)} />
      )}
      <div>
        <p className="text-sm leading-6">{claim.text}</p>
        <div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-zinc-500">
          {claim.refs.map((ref) => (
            <EvidenceRefButton key={ref} id={ref} evidence={evidence} />
          ))}
          {claim.origin === "ai_hypothesis" && claim.confidence ? (
            <p>Уверенность: {confidenceLabels[claim.confidence]}</p>
          ) : null}
          {unreviewed ? (
            <strong className="rounded bg-zinc-200 px-1.5 font-medium text-zinc-700">{reviewStatusLabels[unreviewed]}</strong>
          ) : null}
        </div>
      </div>
    </li>
  );
}

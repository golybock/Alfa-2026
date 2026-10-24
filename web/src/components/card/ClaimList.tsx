import type { ReactNode } from "react";
import type { ClaimDto, EvidenceDto } from "../../lib/types/api";
import ClaimRow from "./ClaimRow";

type ClaimListProps = {
  ids: ReadonlyArray<string>;
  claims: ReadonlyMap<string, ClaimDto>;
  evidence: Readonly<Record<string, EvidenceDto>>;
  stacked?: boolean;
  leading?: ReactNode;
};

export default function ClaimList({ ids, claims, evidence, stacked, leading }: ClaimListProps) {
  if (ids.length === 0) {
    return <p className="text-sm text-zinc-500">Данных нет</p>;
  }

  return (
    <ul className="@container flex flex-col gap-3">
      {ids.map((id) => {
        const claim = claims.get(id);
        return claim ? (
          <ClaimRow key={id} claim={claim} evidence={evidence} stacked={stacked} leading={leading} />
        ) : (
          <li key={id} role="alert" className="text-sm text-alfa">
            Ошибка данных: утверждение {id} отсутствует в карточке
          </li>
        );
      })}
    </ul>
  );
}

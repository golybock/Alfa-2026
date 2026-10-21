import type { ClaimDto, EvidenceDto } from "../../lib/types/api";
import ClaimRow from "./ClaimRow";

type ClaimListProps = {
  ids: ReadonlyArray<string>;
  claims: ReadonlyMap<string, ClaimDto>;
  evidence: Readonly<Record<string, EvidenceDto>>;
};

export default function ClaimList({ ids, claims, evidence }: ClaimListProps) {
  return (
    <ul className="@container flex flex-col gap-3">
      {ids.map((id) => {
        const claim = claims.get(id);
        return claim ? (
          <ClaimRow key={id} claim={claim} evidence={evidence} />
        ) : (
          <li key={id} role="alert" className="text-sm text-alfa">
            Ошибка данных: утверждение {id} отсутствует в карточке
          </li>
        );
      })}
    </ul>
  );
}

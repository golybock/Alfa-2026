import type { ClaimDto, EvidenceDto } from "../../lib/types/api";
import EvidenceRefButton from "../ui/EvidenceRefButton";

type ActionListProps = {
  ids: ReadonlyArray<string>;
  claims: ReadonlyMap<string, ClaimDto>;
  evidence: Readonly<Record<string, EvidenceDto>>;
};

export default function ActionList({ ids, claims, evidence }: ActionListProps) {
  return (
    <ol className="flex flex-col gap-3">
      {ids.map((id, index) => {
        const claim = claims.get(id);
        return (
          <li key={id} className="flex items-start gap-3">
            <em className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-zinc-900 text-xs font-medium not-italic text-white">
              {index + 1}
            </em>
            {claim ? (
              <div>
                <p className="text-sm leading-6">{claim.text}</p>
                <div className="mt-1 flex flex-wrap items-center gap-2">
                  {claim.refs.map((ref) => (
                    <EvidenceRefButton key={ref} id={ref} evidence={evidence} />
                  ))}
                </div>
              </div>
            ) : (
              <p role="alert" className="text-sm text-alfa">
                Ошибка данных: утверждение {id} отсутствует в карточке
              </p>
            )}
          </li>
        );
      })}
    </ol>
  );
}
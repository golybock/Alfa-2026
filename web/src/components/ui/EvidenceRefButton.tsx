"use client";

import { useEvidenceViewer } from "../../lib/hooks/evidenceViewer";
import type { EvidenceMap } from "../../lib/hooks/evidenceViewer";

type EvidenceRefButtonProps = { id: string; evidence: EvidenceMap };

export default function EvidenceRefButton({ id, evidence }: EvidenceRefButtonProps) {
  const open = useEvidenceViewer();

  return (
    <button
      type="button"
      onClick={() => open(evidence, id)}
      title={evidence[id]?.source ?? "Доказательство не найдено"}
      aria-label={`Открыть доказательство ${id}`}
      className="rounded bg-zinc-100 px-1.5 font-mono text-[11px] text-zinc-500 hover:bg-zinc-200 hover:text-zinc-900"
    >
      {id}
    </button>
  );
}

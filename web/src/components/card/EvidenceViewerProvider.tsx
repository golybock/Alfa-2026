"use client";

import { useState } from "react";
import type { ReactNode } from "react";
import { EvidenceViewerContext } from "../../lib/hooks/evidenceViewer";
import type { EvidenceMap } from "../../lib/hooks/evidenceViewer";
import Drawer from "../ui/Drawer";
import EvidenceDetail from "./EvidenceDetail";

type OpenEvidence = { evidence: EvidenceMap; id: string };

type EvidenceViewerProviderProps = { limitations: ReadonlyArray<string>; children: ReactNode };

export default function EvidenceViewerProvider({ limitations, children }: EvidenceViewerProviderProps) {
  const [current, setCurrent] = useState<OpenEvidence | null>(null);
  const item = current ? current.evidence[current.id] : null;

  return (
    <EvidenceViewerContext value={(evidence, id) => setCurrent({ evidence, id })}>
      {children}
      {current ? (
        <Drawer title="Источник" subtitle="Откуда взято утверждение и что о нём известно" onClose={() => setCurrent(null)}>
          {item ? (
            <EvidenceDetail id={current.id} evidence={item} limitations={limitations} />
          ) : (
            <p role="alert" className="text-sm text-alfa">
              Доказательство {current.id} не найдено в карточке
            </p>
          )}
        </Drawer>
      ) : null}
    </EvidenceViewerContext>
  );
}

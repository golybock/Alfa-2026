import { createContext, useContext } from "react";
import type { EvidenceDto } from "../types/api";

export type EvidenceMap = Readonly<Record<string, EvidenceDto>>;

export const EvidenceViewerContext = createContext<((evidence: EvidenceMap, id: string) => void) | null>(null);

export function useEvidenceViewer() {
  const open = useContext(EvidenceViewerContext);
  if (!open) {
    throw new Error("EvidenceViewerContext не подключён");
  }
  return open;
}

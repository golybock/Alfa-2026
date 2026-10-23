import type { ClaimDto, EvidenceDto, EvidenceType } from "./types/api";
import type { IconName } from "./types/icon";

export type ClaimKind = "source_fact" | "company_data" | "bank_analytics" | "ai_inference" | "ai_hypothesis";

type ClaimKindStyle = { label: string; className: string; icon: IconName };

export const claimKinds: Record<ClaimKind, ClaimKindStyle> = {
  source_fact: { label: "Факт из источника", className: "bg-blue-50 text-blue-700", icon: "document" },
  company_data: { label: "Данные компании", className: "bg-teal-50 text-teal-700", icon: "briefcase" },
  bank_analytics: { label: "Позиция Альфа-Банка", className: "bg-red-50 text-alfa", icon: "bank" },
  ai_inference: { label: "Вывод ИИ", className: "bg-violet-50 text-violet-700", icon: "sparkle" },
  ai_hypothesis: { label: "Гипотеза", className: "bg-amber-50 text-orange-600", icon: "help" },
};

export const evidenceKinds: Record<EvidenceType, ClaimKind> = {
  news: "source_fact",
  bank_research: "bank_analytics",
  company_feature: "company_data",
  payment_aggregate: "company_data",
  group_feature: "company_data",
};

const companyEvidenceTypes: EvidenceType[] = ["company_feature", "payment_aggregate", "group_feature"];

export function claimKindOf(claim: ClaimDto, evidence: Readonly<Record<string, EvidenceDto>>): ClaimKind {
  if (claim.origin !== "source_fact") {
    return claim.origin;
  }
  const isCompanyData =
    claim.refs.length > 0 &&
    claim.refs.every((ref) => {
      const type = evidence[ref]?.type;
      return type !== undefined && companyEvidenceTypes.includes(type);
    });
  return isCompanyData ? "company_data" : "source_fact";
}

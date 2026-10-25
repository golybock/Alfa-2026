import type { ConsequenceType } from "../../lib/types/api";
import Icon from "./Icon";

const kinds: Record<ConsequenceType, { label: string; className: string; icon: "trendDown" | "trend" | "info" }> = {
  risk: { label: "Риск", className: "bg-red-50 text-alfa", icon: "trendDown" },
  opportunity: { label: "Возможность", className: "bg-green-50 text-green-700", icon: "trend" },
  neutral: { label: "Нейтрально", className: "bg-gray-100 text-gray-700", icon: "info" },
};

type KindBadgeProps = { kind: ConsequenceType };

export default function KindBadge({ kind }: KindBadgeProps) {
  const { label, className, icon } = kinds[kind];

  return (
    <p className={`flex h-6 w-fit items-center gap-1 rounded-lg px-2 text-xs font-medium ${className}`}>
      <Icon name={icon} className="h-3.5 w-3.5" />
      {label}
    </p>
  );
}

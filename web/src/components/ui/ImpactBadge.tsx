import type { Significance } from "../../lib/types/api";

const levels: Record<Significance, { label: string; filled: number; barClass: string }> = {
  high: { label: "Высокая значимость", filled: 3, barClass: "bg-alfa" },
  medium: { label: "Средняя значимость", filled: 2, barClass: "bg-orange-500" },
  low: { label: "Низкая значимость", filled: 1, barClass: "bg-zinc-400" },
};

const barHeights = ["h-1.5", "h-2.5", "h-3.5"];

type ImpactBadgeProps = { significance: Significance };

export default function ImpactBadge({ significance }: ImpactBadgeProps) {
  const { label, filled, barClass } = levels[significance];

  return (
    <p className="flex h-6 w-fit items-center gap-1.5 rounded-lg bg-zinc-100 px-2 text-xs font-medium text-zinc-900">
      <i aria-hidden="true" className="flex items-end gap-0.5">
        {barHeights.map((height, index) => (
          <i key={height} className={`block w-[3px] rounded-sm ${height} ${index < filled ? barClass : "bg-zinc-300"}`} />
        ))}
      </i>
      {label}
    </p>
  );
}
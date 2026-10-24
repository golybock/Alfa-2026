import type { FeedFilter } from "../../lib/feedFilter";

type FeedFilterTabsProps = {
  filter: FeedFilter;
  counts: Record<FeedFilter, number>;
  onChange: (filter: FeedFilter) => void;
};

const tabs: { value: FeedFilter; title: string }[] = [
  { value: "all", title: "Все инсайты" },
  { value: "risk", title: "Риски" },
  { value: "opportunity", title: "Возможности" },
];

export default function FeedFilterTabs({ filter, counts, onChange }: FeedFilterTabsProps) {
  return (
    <div role="group" aria-label="Фильтр инсайтов" className="flex items-center gap-2">
      {tabs.map((tab) => (
        <button
          key={tab.value}
          type="button"
          aria-pressed={filter === tab.value}
          onClick={() => onChange(tab.value)}
          className={`h-9 rounded-xl px-4 text-xs font-medium transition-colors ${
            filter === tab.value ? "bg-zinc-900 text-white" : "bg-white text-zinc-900 hover:bg-zinc-100"
          }`}
        >
          {tab.title} · {counts[tab.value]}
        </button>
      ))}
    </div>
  );
}
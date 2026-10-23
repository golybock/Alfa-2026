type RankBadgeProps = { rank: number };

export default function RankBadge({ rank }: RankBadgeProps) {
  return (
    <p className="flex h-6 w-fit items-center rounded-lg bg-zinc-100 px-2 text-xs font-medium text-zinc-900">
      №{rank} по значимости
    </p>
  );
}

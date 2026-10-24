import SkeletonBlock from "../ui/SkeletonBlock";

const rows = [0, 1, 2];

export default function FeedSkeleton() {
  return (
    <ul role="status" aria-label="Загружаем ленту" className="mt-3 flex flex-col gap-3">
      {rows.map((row) => (
        <li key={row} className="flex flex-col gap-3 rounded-2xl bg-white px-5 py-5">
          <SkeletonBlock className="h-5 w-40" />
          <SkeletonBlock className="h-4 w-3/4" />
          <SkeletonBlock className="h-4 w-1/2" />
        </li>
      ))}
    </ul>
  );
}
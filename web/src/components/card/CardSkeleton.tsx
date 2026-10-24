import SkeletonBlock from "../ui/SkeletonBlock";

export default function CardSkeleton() {
  return (
    <div role="status" aria-label="Загружаем карточку" className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 rounded-2xl bg-white p-5">
        <SkeletonBlock className="h-5 w-48" />
        <SkeletonBlock className="h-8 w-2/3" />
        <SkeletonBlock className="h-4 w-1/3" />
      </div>
      <div className="grid grid-cols-[1fr_340px] gap-4">
        <div className="flex flex-col gap-4">
          <SkeletonBlock className="h-40 rounded-2xl" />
          <SkeletonBlock className="h-48 rounded-2xl" />
        </div>
        <div className="flex flex-col gap-4">
          <SkeletonBlock className="h-40 rounded-2xl" />
          <SkeletonBlock className="h-32 rounded-2xl" />
        </div>
      </div>
    </div>
  );
}
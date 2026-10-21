import Link from "next/link";
import Icon from "../ui/Icon";

type FeedHeaderProps = { subtitle: string; refreshing: boolean; onRefresh: () => void };

export default function FeedHeader({ subtitle, refreshing, onRefresh }: FeedHeaderProps) {
  return (
    <div className="mb-5 flex items-start justify-between gap-6">
      <div>
        <h1 className="text-3xl font-semibold">Ваши бизнес-инсайты</h1>
        <p className="mt-1 text-sm text-zinc-500">{subtitle}</p>
      </div>
      <div className="flex items-center gap-3">
        <Link
          href="/select"
          className="flex h-9 items-center gap-2 rounded-lg border border-dashed border-blue-600 px-3 text-xs text-blue-700"
        >
          Демо: посмотреть глазами другого клиента
          <Icon name="chevronDown" className="h-4 w-4" />
        </Link>
        <button
          type="button"
          onClick={onRefresh}
          disabled={refreshing}
          className="h-10 rounded-xl bg-zinc-200/60 px-5 text-sm font-medium hover:bg-zinc-200 disabled:opacity-60"
        >
          {refreshing ? "Запускаем обновление…" : "Обновить ленту"}
        </button>
      </div>
    </div>
  );
}

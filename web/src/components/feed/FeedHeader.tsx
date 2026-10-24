import type { FeedMode } from "../../lib/feedMode";
import RoleSwitch from "../ui/RoleSwitch";
import ModeSwitch from "./ModeSwitch";

type FeedHeaderProps = {
  subtitle: string;
  mode: FeedMode;
  snapshotDate: string;
  onModeChange: (mode: FeedMode) => void;
  refreshing: boolean;
  onRefresh: () => void;
};

export default function FeedHeader({ subtitle, mode, snapshotDate, onModeChange, refreshing, onRefresh }: FeedHeaderProps) {
  return (
    <div className="mb-5">
      <div className="flex items-start justify-between gap-6">
        <div className="flex items-center gap-4">
          <h1 className="text-3xl font-semibold">Ваши бизнес-инсайты</h1>
          <ModeSwitch mode={mode} snapshotDate={snapshotDate} onChange={onModeChange} />
        </div>
        <button
          type="button"
          onClick={onRefresh}
          disabled={refreshing}
          className="h-11 rounded-xl bg-zinc-200/60 px-5 text-sm font-medium hover:bg-zinc-200 disabled:opacity-60"
        >
          {refreshing ? "Запускаем обновление…" : "Обновить ленту"}
        </button>
      </div>
      <div className="mt-3 flex items-center justify-between gap-6">
        <p className="text-sm text-zinc-500">{subtitle}</p>
        <RoleSwitch />
      </div>
    </div>
  );
}
import Icon from "../ui/Icon";

type FeedNoticeProps = {
  tone: "info" | "warning";
  title: string;
  text: string;
  actionLabel?: string;
  onAction?: () => void;
};

const toneClass = {
  info: "bg-blue-50 text-blue-700",
  warning: "bg-amber-50 text-orange-700",
};

export default function FeedNotice({ tone, title, text, actionLabel, onAction }: FeedNoticeProps) {
  return (
    <div role={tone === "warning" ? "alert" : "status"} className={`flex items-center gap-4 rounded-2xl px-5 py-4 ${toneClass[tone]}`}>
      <Icon name="info" className="h-5 w-5" />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-zinc-900">{title}</p>
        <p className="mt-0.5 text-xs text-zinc-600">{text}</p>
      </div>
      {actionLabel && onAction ? (
        <button
          type="button"
          onClick={onAction}
          className="h-10 shrink-0 rounded-xl bg-white/70 px-6 text-sm font-medium text-zinc-900 hover:bg-white"
        >
          {actionLabel}
        </button>
      ) : null}
    </div>
  );
}
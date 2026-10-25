import Icon from "../ui/Icon";

export default function InsightReadyNotice() {
  return (
    <div role="status" aria-live="polite" className="page-enter flex items-center gap-3 whitespace-nowrap rounded-xl bg-green-50 px-4 py-2">
      <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-green-600 text-white">
        <Icon name="check" className="h-3.5 w-3.5" />
      </div>
      <p className="text-sm text-green-900">
        <strong className="font-medium">Готов</strong>, собрался успешно, его можно открыть
      </p>
    </div>
  );
}
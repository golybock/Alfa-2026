export default function InsightProgress() {
  return (
    <div role="status" aria-live="polite" className="flex items-center gap-3 whitespace-nowrap rounded-xl bg-blue-50 px-4 py-2">
      <i
        aria-hidden="true"
        className="block h-6 w-6 shrink-0 animate-spin rounded-full border-2 border-blue-200 border-t-blue-600"
      />
      <p className="text-sm text-blue-900">
        <strong className="font-medium">Создаётся</strong>, обычно это занимает не больше 5 минут
      </p>
    </div>
  );
}
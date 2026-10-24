export default function NewsLinkForm() {
  return (
    <div className="mt-6">
      <p className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">Или ссылка на новость</p>
      <div className="mt-2 flex gap-2">
        <input
          type="url"
          disabled
          placeholder="https://"
          aria-label="Ссылка на новость"
          className="h-12 flex-1 rounded-xl border border-zinc-200 px-4 text-sm disabled:cursor-not-allowed disabled:bg-zinc-50"
        />
        <button
          type="button"
          disabled
          className="h-12 rounded-xl bg-alfa px-6 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          Добавить
        </button>
      </div>
      <p className="mt-2 text-xs text-zinc-500">Добавление по ссылке появится позже.</p>
    </div>
  );
}
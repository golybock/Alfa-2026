"use client";

import { useNewsSources } from "../../lib/hooks/queries";
import type { NewsSourceDto } from "../../lib/types/api";
import Icon from "../ui/Icon";

type FeedListProps = { title: string; items: ReadonlyArray<NewsSourceDto> };

function FeedList({ title, items }: FeedListProps) {
  if (items.length === 0) return null;
  return (
    <div>
      <p className="text-xs text-zinc-500">{title}</p>
      <ul className="mt-1 flex list-disc flex-col gap-1 pl-4 text-xs text-zinc-500">
        {items.map((item) => (
          <li key={item.id} className="break-all">
            {item.name}: {item.feed_url}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default function LimitsCard() {
  const sources = useNewsSources();
  const items = sources.data?.items ?? [];

  return (
    <section aria-label="Лимиты и защита" className="rounded-2xl bg-blue-50 p-6">
      <h2 className="flex items-center gap-2 text-sm font-semibold">
        <Icon name="info" className="h-4 w-4 text-blue-600" />
        Лимиты и защита
      </h2>
      <p className="mt-3 text-xs">
        Для документа: до 50 МБ и 50 страниц на файл. Документ будет помечаться моделью как «Позиция банка».
      </p>
      <p className="mt-3 text-xs">
        Ссылки используйте только с источников, которые есть в архитектуре, чтобы модель могла правильно их обработать.
        Ссылки станут отдельным инсайтом.
      </p>
      {sources.isPending ? (
        <p className="mt-3 text-xs text-zinc-500">Загружаем список источников…</p>
      ) : sources.isError ? (
        <p className="mt-3 text-xs text-zinc-500">Список источников недоступен.</p>
      ) : (
        <div className="mt-3 grid grid-cols-2 gap-6">
          <FeedList title="Специализированная лента:" items={items.filter((item) => item.is_specialized)} />
          <FeedList title="Обычная лента:" items={items.filter((item) => !item.is_specialized)} />
        </div>
      )}
    </section>
  );
}

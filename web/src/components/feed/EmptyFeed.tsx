import Icon from "../ui/Icon";

type EmptyFeedProps = { companyName: string; period: string };

export default function EmptyFeed({ companyName, period }: EmptyFeedProps) {
  return (
    <section className="mt-4 flex flex-col items-center rounded-2xl bg-white px-6 py-14 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-zinc-100">
        <Icon name="check" className="h-6 w-6" />
      </div>
      <h2 className="mt-4 text-2xl font-semibold">Значимых событий за {period} нет</h2>
      <p className="mt-3 max-w-md text-sm text-zinc-500">
        Мы не добавляем сигналы ради количества. Как только появится событие, значимое для {companyName}, оно
        появится здесь.
      </p>
    </section>
  );
}

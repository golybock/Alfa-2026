type FeedProgressProps = { companyName: string };

const steps = [
  "Собираем новости за 7 дней",
  "Строим профиль компании по данным банка",
  "Отбираем значимое и пишем карточки",
  "Проверяем расчёты и источники",
];

export default function FeedProgress({ companyName }: FeedProgressProps) {
  return (
    <section role="status" className="rounded-2xl bg-white px-6 py-5">
      <h2 className="text-base font-semibold">Готовим инсайты для {companyName}</h2>
      <ol className="mt-3 flex flex-col gap-2 text-sm text-zinc-600">
        {steps.map((step, index) => (
          <li key={step} className="flex items-center gap-3">
            <em className="flex h-5 w-5 items-center justify-center rounded-full bg-zinc-100 text-[11px] font-medium not-italic text-zinc-700">
              {index + 1}
            </em>
            {step}
          </li>
        ))}
      </ol>
      <p className="mt-3 text-xs text-zinc-500">
        Обычно это занимает от нескольких секунд до двух минут. Уже готовые инсайты можно открыть из сохранённой ленты.
      </p>
    </section>
  );
}
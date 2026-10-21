import type { BankHome } from "../../lib/types/bank";
import Icon from "../ui/Icon";

type BankHomeMockProps = { data: BankHome };

export default function BankHomeMock({ data }: BankHomeMockProps) {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3 rounded-2xl bg-violet-100/60 p-2.5">
        <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-pink-500 to-violet-600 text-white">
          <Icon name="sparkle" className="h-5 w-5" />
        </div>
        <p className="flex-1 rounded-xl bg-white px-4 py-2.5 text-sm text-zinc-500">{data.aiPlaceholder}</p>
      </div>
      <section aria-label="Быстрые действия">
        <h2 className="mb-2 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Быстрые действия</h2>
        <ul className="grid grid-cols-5 gap-3">
          {data.quickActions.map((action) => (
            <li key={action.title} className="flex flex-col gap-3 rounded-2xl bg-white p-4 text-sm">
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-zinc-100">
                <Icon name={action.icon} className="h-4 w-4" />
              </div>
              {action.title}
            </li>
          ))}
        </ul>
      </section>
      <ul className="flex items-center gap-6 rounded-2xl bg-white px-5 py-3.5 text-sm text-zinc-500">
        {data.paymentTabs.map((tab) => (
          <li key={tab.title} className="flex items-center gap-2">
            {tab.title}
            <em className="rounded-full bg-zinc-100 px-2 text-xs not-italic text-zinc-900">{tab.count}</em>
          </li>
        ))}
      </ul>
      <div className="grid grid-cols-[1fr_212px] gap-3">
        <section className="flex gap-10 rounded-2xl bg-white p-5">
          <div>
            <p className="text-sm text-zinc-500">{data.account.title}</p>
            <p className="mt-1 text-3xl font-semibold">{data.account.balance}</p>
            <p className="mt-1 text-xs text-zinc-500">{data.account.number}</p>
            <div className="mt-4 flex items-center gap-4 text-sm">
              <p className="rounded-xl bg-zinc-100 px-4 py-2 font-medium">Реквизиты</p>
              <p className="font-medium">Тарифы и подписки</p>
            </div>
          </div>
          <div>
            <p className="inline-block rounded bg-green-100 px-1.5 py-0.5 text-[10px] font-medium text-green-700">
              {data.account.offerBadge}
            </p>
            <p className="mt-1 text-2xl font-semibold">{data.account.offerAmount}</p>
            <p className="mt-1 text-xs text-zinc-500">{data.account.offerText}</p>
          </div>
          <div className="flex items-start gap-3">
            <p className="flex h-9 w-14 items-center justify-center rounded-md bg-zinc-900 text-xs font-bold text-white">
              A
            </p>
            <div>
              <p className="text-sm font-medium">{data.account.cardName}</p>
              <p className="text-xs text-zinc-500">{data.account.cardNumber}</p>
            </div>
          </div>
        </section>
        <ul className="flex flex-col gap-2">
          {data.promos.map((promo) => (
            <li key={promo.title} className={`rounded-xl px-3 py-2 text-xs ${promo.tone}`}>
              <strong className="block font-medium">{promo.title}</strong>
              {promo.text}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

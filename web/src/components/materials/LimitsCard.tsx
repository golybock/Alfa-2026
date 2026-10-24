import Icon from "../ui/Icon";

const specializedFeeds = [
  "Коммерсантъ: Экономика: https://www.kommersant.ru/RSS/section-economics.xml",
  "Коммерсантъ: Финансы: https://www.kommersant.ru/RSS/section-finance.xml",
  "Ведомости: Экономика: https://www.vedomosti.ru/rss/rubric/economics",
  "Ведомости: Финансы: https://www.vedomosti.ru/rss/rubric/finance",
  "Ведомости: Бизнес: https://www.vedomosti.ru/rss/rubric/business",
];

const regularFeeds = [
  "РБК: https://rssexport.rbc.ru/rbcnews/news/30/full.rss",
  "Минфин России: https://minfin.gov.ru/rss_news?mod=news&lim=50",
  "Правительство РФ: http://government.ru/all/rss/",
  "ФНС России: https://www.nalog.gov.ru/rn77/rss/",
  "Банк России (пресс-релизы): https://www.cbr.ru/rss/RssPress",
];

type FeedListProps = { title: string; items: string[] };

function FeedList({ title, items }: FeedListProps) {
  return (
    <div>
      <p className="text-xs text-zinc-500">{title}</p>
      <ul className="mt-1 flex list-disc flex-col gap-1 pl-4 text-xs text-zinc-500">
        {items.map((item) => (
          <li key={item} className="break-all">
            {item}
          </li>
        ))}
      </ul>
    </div>
  );
}

export default function LimitsCard() {
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
      <div className="mt-3 grid grid-cols-2 gap-6">
        <FeedList title="Специализированная лента:" items={specializedFeeds} />
        <FeedList title="Обычная лента:" items={regularFeeds} />
      </div>
    </section>
  );
}
"use client";

import Link from "next/link";
import { useState } from "react";
import { useMe } from "../../lib/hooks/session";
import { ANALYTICS_HREF } from "../../lib/navigation";
import FeedNotice from "../feed/FeedNotice";
import Icon from "../ui/Icon";
import LimitsCard from "./LimitsCard";
import type { LinkSubmission } from "./LinkSubmissionRow";
import UploadCard from "./UploadCard";
import UploadsList from "./UploadsList";

export default function MaterialsScreen() {
  const me = useMe();
  const canAdd = me.data?.can_add_materials ?? false;
  const [links, setLinks] = useState<ReadonlyArray<LinkSubmission>>([]);

  return (
    <div>
      <Link href={ANALYTICS_HREF} className="flex w-fit items-center gap-1 text-xs text-zinc-600 hover:text-zinc-900">
        <Icon name="chevronLeft" className="h-3.5 w-3.5" />
        Ваши бизнес-инсайты
      </Link>
      <h1 className="mt-3 text-3xl font-semibold">Добавить материал</h1>
      <p className="mt-2 max-w-3xl text-sm text-zinc-500">
        Для жюри: добавьте ссылку на новость или PDF. Система покажет результат в ленте инсайтов или объяснит, почему
        материал не попал.
      </p>
      {me.isSuccess && !canAdd ? (
        <div className="mt-4">
          <FeedNotice
            tone="warning"
            title="Добавление материалов недоступно"
            text="Для вашей учётной записи добавление документов и ссылок не включено."
          />
        </div>
      ) : null}
      <div className="mt-4 grid grid-cols-[minmax(0,1.6fr)_minmax(0,1fr)] items-start gap-4">
        <div className="flex flex-col gap-4">
          <UploadCard
            disabled={!canAdd}
            onNewsAdded={(url, jobId) => setLinks([{ url, jobId, addedAt: new Date().toISOString() }, ...links])}
          />
          <LimitsCard />
        </div>
        <UploadsList enabled={canAdd} links={links} />
      </div>
    </div>
  );
}
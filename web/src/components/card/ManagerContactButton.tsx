"use client";

import { useState } from "react";
import { describeError } from "../../lib/api/client";
import { useRecordActivity } from "../../lib/hooks/queries";
import type { InsightDto } from "../../lib/types/api";

type ManagerContactButtonProps = { insight: InsightDto };

export default function ManagerContactButton({ insight }: ManagerContactButtonProps) {
  const [open, setOpen] = useState(false);
  const activity = useRecordActivity(insight.id);

  const onOpen = () => {
    setOpen(true);
    activity.mutate({
      event_id: crypto.randomUUID(),
      type: "manager_contact_requested",
      evidence_id: null,
      action_id: null,
    });
  };

  return (
    <>
      <button
        type="button"
        onClick={onOpen}
        className="mt-4 h-10 rounded-xl bg-zinc-100 px-4 text-sm font-medium hover:bg-zinc-200"
      >
        Связаться с менеджером
      </button>
      {open ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-900/50">
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="manager-contact-title"
            className="w-[440px] rounded-3xl bg-white p-7 text-zinc-900 shadow-xl"
          >
            <h2 id="manager-contact-title" className="text-xl font-semibold">
              Связаться с менеджером
            </h2>
            <p className="mt-3 text-sm text-zinc-600">
              В демо заявка не отправляется. В рабочей версии она уйдёт вашему менеджеру.
            </p>
            {activity.isError ? (
              <p role="alert" className="mt-3 text-xs text-alfa">
                Не удалось записать действие: {describeError(activity.error)}
              </p>
            ) : null}
            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="h-11 rounded-xl bg-zinc-900 px-6 text-sm font-medium text-white hover:bg-zinc-800"
              >
                Понятно
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </>
  );
}
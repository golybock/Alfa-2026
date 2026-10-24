"use client";

import { useActiveContext, useSelectContext } from "../../lib/hooks/session";
import { roleShortLabels } from "../../lib/labels";

export default function RoleSwitch() {
  const active = useActiveContext();
  const selectContext = useSelectContext();
  if (!active || active.company.roles.length < 2) {
    return null;
  }

  return (
    <div
      role="group"
      aria-label="Роль"
      className="flex h-8 shrink-0 items-center gap-0.5 rounded-lg bg-zinc-200/60 p-0.5"
    >
      {active.company.roles.map((role) => (
        <button
          key={role}
          type="button"
          aria-pressed={role === active.context.role}
          onClick={() => selectContext({ companyId: active.company.company_id, role })}
          className={`h-7 rounded-md px-2.5 text-xs font-medium ${
            role === active.context.role ? "bg-zinc-900 text-white" : "text-zinc-700 hover:bg-zinc-100"
          }`}
        >
          {roleShortLabels[role]}
        </button>
      ))}
    </div>
  );
}
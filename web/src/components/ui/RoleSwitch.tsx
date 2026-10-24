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
      className="flex h-11 items-center gap-1 rounded-xl border border-dashed border-blue-600 px-2"
    >
      {active.company.roles.map((role) => (
        <button
          key={role}
          type="button"
          aria-pressed={role === active.context.role}
          onClick={() => selectContext({ companyId: active.company.company_id, role })}
          className={`h-8 rounded-lg px-3 text-xs font-medium ${
            role === active.context.role ? "bg-zinc-900 text-white" : "text-zinc-900 hover:bg-zinc-100"
          }`}
        >
          {roleShortLabels[role]}
        </button>
      ))}
    </div>
  );
}
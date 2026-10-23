"use client";

import Link from "next/link";
import { useState } from "react";
import type { FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useSelectContext } from "../../lib/hooks/session";
import type { ScreenContext } from "../../lib/hooks/session";
import { roleLabels } from "../../lib/labels";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { MeResponse, Role } from "../../lib/types/api";

type CompanyRoleFormProps = { me: MeResponse; current: ScreenContext | null };

export default function CompanyRoleForm({ me, current }: CompanyRoleFormProps) {
  const router = useRouter();
  const selectContext = useSelectContext();
  const initialCompany = me.companies.find((company) => company.company_id === current?.companyId) ?? me.companies[0];
  const [companyId, setCompanyId] = useState(initialCompany?.company_id ?? "");
  const [role, setRole] = useState<Role | null>(
    current && initialCompany?.roles.includes(current.role) ? current.role : (initialCompany?.roles[0] ?? null),
  );
  const company = me.companies.find((candidate) => candidate.company_id === companyId);

  const chooseCompany = (nextId: string) => {
    const next = me.companies.find((candidate) => candidate.company_id === nextId);
    setCompanyId(nextId);
    if (next && (!role || !next.roles.includes(role))) {
      setRole(next.roles[0] ?? null);
    }
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!company || !role) {
      return;
    }
    selectContext({ companyId: company.company_id, role });
    router.push(ANALYTICS_HREF);
  };

  if (me.companies.length === 0) {
    return <p className="text-sm text-zinc-600">У вашей учётной записи нет доступных компаний.</p>;
  }

  return (
    <form onSubmit={onSubmit}>
      <fieldset>
        <legend className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">
          Компания или группа
        </legend>
        <div className="flex flex-col gap-3">
          {me.companies.map((option) => (
            <label
              key={option.company_id}
              className={`flex cursor-pointer items-center gap-3 rounded-xl border px-5 py-4 ${
                companyId === option.company_id ? "border-2 border-zinc-900 bg-canvas" : "border-zinc-200"
              }`}
            >
              <input
                type="radio"
                name="company"
                value={option.company_id}
                checked={companyId === option.company_id}
                onChange={() => chooseCompany(option.company_id)}
                className="h-4 w-4 accent-alfa"
              />
              <div>
                <p className="text-sm font-medium">{option.name}</p>
                <p className="text-xs text-zinc-500">{option.company_id}</p>
              </div>
            </label>
          ))}
        </div>
      </fieldset>
      <fieldset className="mt-6">
        <legend className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Роль</legend>
        <div className="grid grid-cols-2 gap-3">
          {(company?.roles ?? []).map((option) => (
            <label
              key={option}
              className={`flex h-11 cursor-pointer items-center justify-center rounded-xl text-sm font-medium ${
                role === option ? "bg-zinc-900 text-white" : "bg-canvas"
              }`}
            >
              <input
                type="radio"
                name="role"
                value={option}
                checked={role === option}
                onChange={() => setRole(option)}
                className="sr-only"
              />
              {roleLabels[option]}
            </label>
          ))}
        </div>
        <p className="mt-3 text-xs text-zinc-500">
          Роль меняет акценты и вопросы в карточке. Список и порядок событий у компании общий.
        </p>
      </fieldset>
      <div className="mt-6 flex justify-end gap-3">
        {current ? (
          <Link href="/" className="flex h-11 items-center rounded-xl border border-zinc-200 px-6 text-sm font-medium">
            Отмена
          </Link>
        ) : null}
        <button
          type="submit"
          disabled={!company || !role}
          className="flex h-11 items-center rounded-xl bg-alfa px-6 text-sm font-medium text-white disabled:opacity-60"
        >
          Показать инсайты
        </button>
      </div>
    </form>
  );
}

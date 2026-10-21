"use client";

import Link from "next/link";
import { useState } from "react";
import { ANALYTICS_HREF } from "../../lib/navigation";
import type { SelectionData } from "../../lib/types/bank";
import ModalFrame from "../ui/ModalFrame";

type CompanyRoleModalProps = { data: SelectionData };

export default function CompanyRoleModal({ data }: CompanyRoleModalProps) {
  const [companyId, setCompanyId] = useState(data.companies[0]?.id ?? "");
  const [roleId, setRoleId] = useState(data.roles[0]?.id ?? "");

  return (
    <ModalFrame title="Выберите компанию и роль" step={2} closeHref="/login">
      <fieldset>
        <legend className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">
          Компания или группа
        </legend>
        <div className="flex flex-col gap-3">
          {data.companies.map((company) => (
            <label
              key={company.id}
              className={`flex cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 ${
                companyId === company.id ? "border-2 border-zinc-900 bg-canvas" : "border-zinc-200"
              }`}
            >
              <input
                type="radio"
                name="company"
                value={company.id}
                checked={companyId === company.id}
                onChange={() => setCompanyId(company.id)}
                className="h-4 w-4 accent-alfa"
              />
              <div>
                <p className="text-sm font-medium">{company.name}</p>
                <p className="text-xs text-zinc-500">{company.description}</p>
              </div>
            </label>
          ))}
        </div>
      </fieldset>
      <fieldset className="mt-6">
        <legend className="mb-3 text-[11px] font-medium uppercase tracking-wider text-zinc-500">Роль</legend>
        <div className="grid grid-cols-2 gap-3">
          {data.roles.map((role) => (
            <label
              key={role.id}
              className={`flex h-11 cursor-pointer items-center justify-center rounded-xl text-sm font-medium ${
                roleId === role.id ? "bg-zinc-900 text-white" : "bg-canvas"
              }`}
            >
              <input
                type="radio"
                name="role"
                value={role.id}
                checked={roleId === role.id}
                onChange={() => setRoleId(role.id)}
                className="sr-only"
              />
              {role.title}
            </label>
          ))}
        </div>
        <p className="mt-3 text-xs text-zinc-500">Роль меняет порядок сигналов, угол зрения и предлагаемые действия</p>
      </fieldset>
      <div className="mt-6 flex justify-end gap-3">
        <Link href="/login" className="flex h-11 items-center rounded-xl border border-zinc-200 px-6 text-sm font-medium">
          Отмена
        </Link>
        <Link href={ANALYTICS_HREF} className="flex h-11 items-center rounded-xl bg-alfa px-6 text-sm font-medium text-white">
          Показать сигналы
        </Link>
      </div>
    </ModalFrame>
  );
}

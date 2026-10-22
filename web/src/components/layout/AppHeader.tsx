"use client";

import { useRouter } from "next/navigation";
import { useLogout } from "../../lib/hooks/session";
import { roleLabels } from "../../lib/labels";
import type { CompanyAccessDto, MeResponse, Role } from "../../lib/types/api";
import BrandMark from "./BrandMark";
import HeaderIconButton from "./HeaderIconButton";

type AppHeaderProps = { me: MeResponse; company: CompanyAccessDto; role: Role };

export default function AppHeader({ me, company, role }: AppHeaderProps) {
  const router = useRouter();
  const logout = useLogout();

  return (
    <header className="flex h-16 shrink-0 items-center justify-between px-5 text-zinc-900">
      <BrandMark />
      <div className="flex items-center gap-1">
        <HeaderIconButton icon="search" label="Поиск" />
        <HeaderIconButton icon="mail" label="Сообщения" />
        <HeaderIconButton icon="bell" label="Уведомления" />
        <div className="mx-3 flex items-center gap-3">
          <div aria-hidden className="h-8 w-8 rounded-full bg-amber-100" />
          <div>
            <p className="text-xs font-medium">{me.display_name}</p>
            <p className="text-xs text-zinc-500">
              {roleLabels[role]} · {company.name}
            </p>
          </div>
        </div>
        <HeaderIconButton icon="briefcase" label="Сменить компанию или роль" href="/select" />
        <HeaderIconButton icon="panel" label="Боковая панель" />
        <button
          type="button"
          disabled={logout.isPending}
          onClick={() => logout.mutate(undefined, { onSettled: () => router.replace("/login") })}
          className="ml-2 h-9 rounded-lg bg-white px-4 text-sm font-medium transition-colors hover:bg-zinc-100 disabled:opacity-60"
        >
          {logout.isPending ? "Выходим…" : "Выйти"}
        </button>
      </div>
    </header>
  );
}

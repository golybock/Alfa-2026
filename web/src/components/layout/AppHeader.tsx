import { logoutAction } from "../../app/login/actions";
import { roleLabels } from "../../lib/labels";
import type { SessionUser } from "../../lib/types/auth";
import BrandMark from "./BrandMark";
import HeaderIconButton from "./HeaderIconButton";

type AppHeaderProps = { user: SessionUser };

export default function AppHeader({ user }: AppHeaderProps) {
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
            <p className="text-xs font-medium">{user.displayName}</p>
            <p className="text-xs text-zinc-500">
              {roleLabels[user.role]} · {user.companyName}
            </p>
          </div>
        </div>
        <HeaderIconButton icon="briefcase" label="Компании" />
        <HeaderIconButton icon="panel" label="Боковая панель" />
        <form action={logoutAction}>
          <button
            type="submit"
            className="ml-2 h-9 rounded-lg bg-white px-4 text-sm font-medium transition-colors hover:bg-zinc-100"
          >
            Выйти
          </button>
        </form>
      </div>
    </header>
  );
}

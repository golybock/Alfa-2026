import { logoutAction } from "../app/login/actions";
import type { SessionUser } from "../lib/types/auth";

type AppHeaderProps = { user: SessionUser };

export default function AppHeader({ user }: AppHeaderProps) {
  return (
    <header className="flex h-16 items-center justify-between border-b border-zinc-200 bg-white px-6 text-zinc-900">
      <div className="flex items-center gap-3">
        <div aria-hidden className="flex h-8 w-8 items-center justify-center rounded-md bg-alfa text-lg font-bold text-white">
          А
        </div>
        <strong className="text-lg">Альфа-Бизнес</strong>
      </div>
      <div className="flex items-center gap-4">
        <div className="text-right">
          <p className="text-sm font-medium">{user.companyName}</p>
          <p className="text-xs text-zinc-500">
            {user.displayName}, {user.role}
          </p>
        </div>
        <form action={logoutAction}>
          <button
            type="submit"
            className="h-9 rounded-lg border border-zinc-300 px-4 text-sm font-medium transition-colors hover:bg-zinc-100"
          >
            Выйти
          </button>
        </form>
      </div>
    </header>
  );
}

import type { ReactNode } from "react";
import type { CompanyAccessDto, MeResponse, Role } from "../../lib/types/api";
import AppHeader from "./AppHeader";
import SideMenu from "./SideMenu";

type AppShellProps = { me: MeResponse; company: CompanyAccessDto; role: Role; children: ReactNode };

export default function AppShell({ me, company, role, children }: AppShellProps) {
  return (
    <div className="flex h-screen min-w-[1280px] flex-col bg-canvas">
      <AppHeader me={me} company={company} role={role} />
      <div className="flex min-h-0 flex-1">
        <SideMenu />
        <main className="min-w-0 flex-1 overflow-y-auto px-6 pb-8 pt-4 text-zinc-900">{children}</main>
      </div>
    </div>
  );
}

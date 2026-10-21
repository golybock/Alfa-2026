import { getBankHome } from "../../lib/api/bank";
import BankHomeMock from "./BankHomeMock";
import BrandMark from "../layout/BrandMark";
import SideMenu from "../layout/SideMenu";

export default function BankBackdrop() {
  return (
    <div inert aria-hidden className="flex h-screen min-w-[1280px] select-none flex-col bg-canvas text-zinc-900">
      <header className="flex h-16 shrink-0 items-center px-5">
        <BrandMark />
      </header>
      <div className="flex min-h-0 flex-1">
        <SideMenu />
        <main className="min-w-0 flex-1 overflow-hidden px-6 pt-4">
          <BankHomeMock data={getBankHome()} />
        </main>
      </div>
    </div>
  );
}

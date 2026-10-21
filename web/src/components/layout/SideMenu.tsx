import { bankingMenu, primaryMenu, settingsMenuItem } from "../../lib/navigation";
import SideMenuLink from "./SideMenuLink";

export default function SideMenu() {
  return (
    <nav aria-label="Основное меню" className="flex w-60 shrink-0 flex-col justify-between px-4 py-4 text-zinc-900">
      <div className="flex flex-col gap-1">
        {primaryMenu.map((item) => (
          <SideMenuLink key={item.href} item={item} />
        ))}
        <hr className="my-3 border-zinc-200" />
        {bankingMenu.map((item) => (
          <SideMenuLink key={item.href} item={item} />
        ))}
      </div>
      <SideMenuLink item={settingsMenuItem} />
    </nav>
  );
}

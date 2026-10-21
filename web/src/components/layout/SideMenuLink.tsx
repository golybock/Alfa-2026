"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { MenuItem } from "../../lib/navigation";
import Icon from "../ui/Icon";

type SideMenuLinkProps = { item: MenuItem };

export default function SideMenuLink({ item }: SideMenuLinkProps) {
  const pathname = usePathname();
  const active = pathname === item.href || pathname.startsWith(`${item.href}/`);

  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={`flex h-10 items-center gap-3 rounded-xl px-3 text-sm transition-colors hover:bg-zinc-200/60 ${
        active ? "bg-zinc-200/70 font-medium" : ""
      }`}
    >
      <Icon name={item.icon} className={`h-5 w-5 ${active ? "text-alfa" : ""}`} />
      {item.title}
    </Link>
  );
}

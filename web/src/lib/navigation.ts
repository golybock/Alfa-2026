import type { IconName } from "./types/icon";

export type MenuItem = {
  href: string;
  title: string;
  icon: IconName;
};

export const ANALYTICS_HREF = "/analytics";

export const primaryMenu: MenuItem[] = [
  { href: "/sections/services", title: "Все сервисы", icon: "grid" },
  { href: "/sections/benefits", title: "Альфа-Выгодно", icon: "sparkle" },
];

export const bankingMenu: MenuItem[] = [
  { href: "/sections/new-payment", title: "Новый платёж", icon: "plus" },
  { href: "/sections/accounts", title: "Счета", icon: "document" },
  { href: "/sections/counterparties", title: "Контрагенты", icon: "users" },
  { href: "/sections/credits", title: "Кредитные продукты", icon: "percent" },
  { href: ANALYTICS_HREF, title: "Бизнес-инсайты", icon: "trend" },
];

export const settingsMenuItem: MenuItem = {
  href: "/sections/menu-settings",
  title: "Настроить меню",
  icon: "sliders",
};

export const stubSections: MenuItem[] = [...primaryMenu, ...bankingMenu, settingsMenuItem].filter(
  (item) => item.href !== ANALYTICS_HREF,
);

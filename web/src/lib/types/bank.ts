import type { IconName } from "./icon";

export type BankHome = {
  aiPlaceholder: string;
  quickActions: { title: string; icon: IconName }[];
  paymentTabs: { title: string; count: number }[];
  account: {
    title: string;
    balance: string;
    number: string;
    offerBadge: string;
    offerAmount: string;
    offerText: string;
    cardName: string;
    cardNumber: string;
  };
  promos: { title: string; text: string; tone: string }[];
};

export type CompanyOption = { id: string; name: string; description: string };

export type RoleOption = { id: string; title: string };

export type SelectionData = { companies: CompanyOption[]; roles: RoleOption[] };

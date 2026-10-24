import type { ReactNode } from "react";

export default function BankTemplate({ children }: { children: ReactNode }) {
  return <div className="page-enter">{children}</div>;
}
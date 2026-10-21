import { Suspense } from "react";
import AuthedShell from "../../components/layout/AuthedShell";

export default function BankLayout({ children }: LayoutProps<"/">) {
  return (
    <Suspense fallback={null}>
      <AuthedShell>{children}</AuthedShell>
    </Suspense>
  );
}

import { Suspense } from "react";
import BankHomeMock from "../../components/bank/BankHomeMock";
import InsightsWidgetScreen from "../../components/feed/InsightsWidgetScreen";
import { getBankHome } from "../../lib/api/bank";

export const instant = false;

export default function Home() {
  return (
    <>
      <BankHomeMock data={getBankHome()} />
      <Suspense fallback={null}>
        <InsightsWidgetScreen />
      </Suspense>
    </>
  );
}

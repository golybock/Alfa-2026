import { Suspense } from "react";
import InsightCardScreen from "../../../../components/card/InsightCardScreen";

export const instant = false;

export default function InsightCardPage({ params }: PageProps<"/analytics/[eventId]">) {
  return (
    <Suspense fallback={null}>
      <InsightCardScreen params={params} />
    </Suspense>
  );
}

import { Suspense } from "react";
import FeedScreen from "../../../components/feed/FeedScreen";

export const instant = false;

export default function AnalyticsPage() {
  return (
    <Suspense fallback={null}>
      <FeedScreen />
    </Suspense>
  );
}

import { Suspense } from "react";
import FeedScreen from "../../../components/feed/FeedScreen";

export default function AnalyticsPage() {
  return (
    <Suspense fallback={null}>
      <FeedScreen />
    </Suspense>
  );
}

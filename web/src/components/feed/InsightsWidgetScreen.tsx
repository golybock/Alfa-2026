import { requireCurrentUser } from "../../lib/api/auth";
import InsightsWidget from "./InsightsWidget";

export default async function InsightsWidgetScreen() {
  const user = await requireCurrentUser();

  return <InsightsWidget companyId={user.companyId} role={user.role} />;
}

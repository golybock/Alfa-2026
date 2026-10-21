import { requireCurrentUser } from "../../lib/api/auth";
import InsightsFeed from "./InsightsFeed";

export default async function FeedScreen() {
  const user = await requireCurrentUser();

  return <InsightsFeed companyId={user.companyId} companyName={user.companyName} role={user.role} />;
}

import { requireCurrentUser } from "../../lib/api/auth";
import InsightCardContent from "./InsightCardContent";

type InsightCardScreenProps = { params: Promise<{ eventId: string }> };

export default async function InsightCardScreen({ params }: InsightCardScreenProps) {
  const { eventId } = await params;
  const user = await requireCurrentUser();

  return <InsightCardContent companyId={user.companyId} eventId={eventId} role={user.role} />;
}

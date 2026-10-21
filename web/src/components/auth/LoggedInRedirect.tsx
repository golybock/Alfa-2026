import { redirect } from "next/navigation";
import { getCurrentUser } from "../../lib/api/auth";

export default async function LoggedInRedirect() {
  const user = await getCurrentUser();
  if (user) {
    redirect("/");
  }
  return null;
}

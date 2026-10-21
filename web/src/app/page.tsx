import { redirect } from "next/navigation";
import AppHeader from "../components/AppHeader";
import { getCurrentUser } from "../lib/api/auth";

export default async function Home() {
  const user = await getCurrentUser();
  if (!user) {
    redirect("/login");
  }

  return (
    <>
      <AppHeader user={user} />
      <main className="flex-1 bg-zinc-50 p-6 text-zinc-900">
        <h1 className="text-2xl font-semibold">Новостная аналитика</h1>
        <p className="mt-2 text-zinc-600">События для компании «{user.companyName}» появятся здесь.</p>
      </main>
    </>
  );
}

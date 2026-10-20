import { redirect } from "next/navigation";
import LoginForm from "../../components/LoginForm";
import { getCurrentUser } from "../../lib/api/auth";

export default async function LoginPage() {
  const user = await getCurrentUser();
  if (user) {
    redirect("/");
  }

  return (
    <main className="flex flex-1 items-center justify-center bg-zinc-50 px-4 py-16">
      <section className="w-full max-w-sm rounded-2xl bg-white p-8 text-zinc-900 shadow-sm">
        <h1 className="mb-6 text-2xl font-semibold">Вход в Альфа-Бизнес</h1>
        <LoginForm />
      </section>
    </main>
  );
}

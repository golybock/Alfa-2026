import Link from "next/link";

type AccessDeniedProps = { companyId: string };

export default function AccessDenied({ companyId }: AccessDeniedProps) {
  return (
    <div className="flex h-screen items-center justify-center bg-canvas p-6 text-zinc-900">
      <section role="alert" className="w-[480px] rounded-3xl bg-white p-7 text-center">
        <h1 className="text-2xl font-semibold">Нет доступа к компании</h1>
        <p className="mt-3 text-sm text-zinc-600">
          Компания <code className="rounded bg-zinc-100 px-1.5">{companyId}</code> не входит в список доступных вашей
          учётной записи или выбранная роль для неё не разрешена.
        </p>
        <Link
          href="/select"
          className="mt-6 inline-flex h-11 items-center rounded-xl bg-alfa px-6 text-sm font-medium text-white"
        >
          Выбрать доступную компанию
        </Link>
      </section>
    </div>
  );
}

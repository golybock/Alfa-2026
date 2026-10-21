"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { describeError } from "../../lib/api/client";
import { useSession } from "../../lib/hooks/session";
import ModalFrame from "../ui/ModalFrame";
import CompanyRoleForm from "./CompanyRoleForm";

export default function CompanyRoleModal() {
  const router = useRouter();
  const session = useSession();

  useEffect(() => {
    if (session.status === "unauthorized") {
      router.replace("/login");
    }
  }, [session.status, router]);

  const current = session.status === "ready" || session.status === "forbidden" ? session.context : null;

  return (
    <ModalFrame title="Выберите компанию и роль" step={2} closeHref={current ? "/" : "/login"}>
      {session.status === "loading" || session.status === "unauthorized" ? (
        <p className="text-sm text-zinc-500">Загружаем доступные компании…</p>
      ) : null}
      {session.status === "failed" ? (
        <p role="alert" className="text-sm text-alfa">
          Не удалось загрузить доступные компании: {describeError(session.error)}
        </p>
      ) : null}
      {session.status === "unselected" || session.status === "ready" || session.status === "forbidden" ? (
        <CompanyRoleForm me={session.me} current={current} />
      ) : null}
    </ModalFrame>
  );
}

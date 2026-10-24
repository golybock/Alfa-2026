"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useMe } from "../../lib/hooks/session";

export default function LoggedInRedirect() {
  const router = useRouter();
  const me = useMe();

  useEffect(() => {
    if (me.isSuccess) {
      router.replace("/");
    }
  }, [me.isSuccess, router]);

  return null;
}

"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useMe } from "../../lib/hooks/session";
import { ANALYTICS_HREF } from "../../lib/navigation";

export default function LoggedInRedirect() {
  const router = useRouter();
  const me = useMe();

  useEffect(() => {
    if (me.isSuccess) {
      router.replace(ANALYTICS_HREF);
    }
  }, [me.isSuccess, router]);

  return null;
}

import Link from "next/link";
import type { IconName } from "../../lib/types/icon";
import Icon from "../ui/Icon";

type HeaderIconButtonProps = { icon: IconName; label: string; href?: string };

const buttonClassName =
  "flex h-10 w-10 items-center justify-center rounded-lg text-zinc-800 transition-colors hover:bg-zinc-200/60";

export default function HeaderIconButton({ icon, label, href }: HeaderIconButtonProps) {
  if (href) {
    return (
      <Link href={href} aria-label={label} title={label} className={buttonClassName}>
        <Icon name={icon} className="h-5 w-5" />
      </Link>
    );
  }

  return (
    <button type="button" aria-label={label} className={buttonClassName}>
      <Icon name={icon} className="h-5 w-5" />
    </button>
  );
}

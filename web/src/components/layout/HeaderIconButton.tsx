import type { IconName } from "../../lib/types/icon";
import Icon from "../ui/Icon";

type HeaderIconButtonProps = { icon: IconName; label: string };

export default function HeaderIconButton({ icon, label }: HeaderIconButtonProps) {
  return (
    <button
      type="button"
      aria-label={label}
      className="flex h-10 w-10 items-center justify-center rounded-lg text-zinc-800 transition-colors hover:bg-zinc-200/60"
    >
      <Icon name={icon} className="h-5 w-5" />
    </button>
  );
}

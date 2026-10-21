import { claimKinds } from "../../lib/claimKinds";
import type { ClaimKind } from "../../lib/claimKinds";
import Icon from "./Icon";

type ClaimBadgeProps = { kind: ClaimKind };

export default function ClaimBadge({ kind }: ClaimBadgeProps) {
  const { label, className, icon } = claimKinds[kind];

  return (
    <p className={`inline-flex h-6 w-fit items-center gap-1 rounded-md px-2 text-[11px] font-medium ${className}`}>
      <Icon name={icon} className="h-3 w-3" />
      {label}
    </p>
  );
}

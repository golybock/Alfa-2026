import ClaimBadge from "../ui/ClaimBadge";
import Icon from "../ui/Icon";

export default function NoActionsNotice() {
  return (
    <div className="flex flex-col gap-2">
      <ClaimBadge kind="ai_inference" />
      <p className="text-sm text-zinc-900">Не рекомендуется никаких действий</p>
      <div className="mt-1 flex flex-col items-center gap-4 rounded-2xl bg-zinc-100 px-6 py-8">
        <div className="flex h-20 w-20 items-center justify-center rounded-full bg-zinc-200 text-zinc-900">
          <Icon name="check" className="h-7 w-7" />
        </div>
        <p className="text-sm text-zinc-900">Модель считает, что все хорошо!</p>
      </div>
    </div>
  );
}
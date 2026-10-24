type UploadProgressBarProps = { fraction: number; label: string };

export default function UploadProgressBar({ fraction, label }: UploadProgressBarProps) {
  const percent = Math.round(Math.min(1, Math.max(0, fraction)) * 100);

  return (
    <div className="w-full">
      <div
        role="progressbar"
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        className="h-1.5 w-full overflow-hidden rounded-full bg-zinc-200"
      >
        <div className="h-full rounded-full bg-blue-600 transition-[width] duration-200" style={{ width: `${percent}%` }} />
      </div>
      <p className="mt-2 text-xs text-zinc-500">
        {label} · {percent}%
      </p>
    </div>
  );
}
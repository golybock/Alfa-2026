type SkeletonBlockProps = { className: string };

export default function SkeletonBlock({ className }: SkeletonBlockProps) {
  return <i aria-hidden="true" className={`block animate-pulse rounded-lg bg-zinc-100 ${className}`} />;
}
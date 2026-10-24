import type { ReactNode } from "react";

type CardBlockProps = { title: string; subtitle?: string; className?: string; children: ReactNode };

export default function CardBlock({ title, subtitle, className = "", children }: CardBlockProps) {
  return (
    <section aria-label={title} className={`rounded-2xl bg-white p-5 ${className}`}>
      <h2 className="text-base font-semibold">{title}</h2>
      {subtitle ? <p className="mt-1 text-xs text-zinc-500">{subtitle}</p> : null}
      <div className="mt-3">{children}</div>
    </section>
  );
}
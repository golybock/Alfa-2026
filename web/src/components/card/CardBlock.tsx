import type { ReactNode } from "react";

type CardBlockProps = { number: number; title: string; children: ReactNode };

export default function CardBlock({ number, title, children }: CardBlockProps) {
  const headingId = `block-${number}`;

  return (
    <section aria-labelledby={headingId} className="rounded-2xl bg-white p-5">
      <div className="mb-3 flex items-center gap-2">
        <p aria-hidden className="flex h-6 w-6 items-center justify-center rounded-full bg-zinc-900 text-xs text-white">
          {number}
        </p>
        <h2 id={headingId} className="text-base font-semibold">
          {title}
        </h2>
      </div>
      {children}
    </section>
  );
}

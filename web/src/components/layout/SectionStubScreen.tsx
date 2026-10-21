import { notFound } from "next/navigation";
import { stubSections } from "../../lib/navigation";

type SectionStubScreenProps = { params: Promise<{ section: string }> };

export default async function SectionStubScreen({ params }: SectionStubScreenProps) {
  const { section } = await params;
  const item = stubSections.find((candidate) => candidate.href === `/sections/${section}`);
  if (!item) {
    notFound();
  }

  return (
    <section aria-labelledby="section-title">
      <h1 id="section-title" className="text-2xl font-semibold">
        {item.title}
      </h1>
      <p className="mt-6 rounded-xl bg-white p-6 text-center text-sm text-zinc-500">Раздел банка не входит в демо</p>
    </section>
  );
}

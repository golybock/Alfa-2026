import { Suspense } from "react";
import SectionStubScreen from "../../../../components/layout/SectionStubScreen";
import { stubSections } from "../../../../lib/navigation";

export const instant = false;

export function generateStaticParams() {
  return stubSections.map((item) => ({ section: item.href.split("/").pop() ?? "" }));
}

export default function SectionStubPage({ params }: PageProps<"/sections/[section]">) {
  return (
    <Suspense fallback={null}>
      <SectionStubScreen params={params} />
    </Suspense>
  );
}

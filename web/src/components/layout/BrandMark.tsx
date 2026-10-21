import Image from "next/image";

export default function BrandMark() {
  return (
    <div className="flex items-center gap-3">
      <Image src="/alfa-logo.jpg" alt="Альфа-Банк" width={32} height={32} className="h-8 w-8 rounded-md" priority />
      <strong className="text-base font-semibold">Альфа-Бизнес</strong>
    </div>
  );
}

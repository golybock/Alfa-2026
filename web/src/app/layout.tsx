import type { Metadata } from "next";
import { Suspense } from "react";
import Providers from "./providers";
import "./globals.css";

export const metadata: Metadata = {
  title: "Альфа-Бизнес",
  description: "Новостная аналитика",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ru" className="h-full antialiased">
      <body className="min-h-full flex flex-col">
        <Providers>
          <Suspense fallback={<p className="p-6">Загрузка…</p>}>{children}</Suspense>
        </Providers>
      </body>
    </html>
  );
}

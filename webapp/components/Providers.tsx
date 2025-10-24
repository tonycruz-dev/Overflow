'use client';
import * as React from "react";
// 1. import `HeroUIProvider` component
import { HeroUIProvider } from "@heroui/react";
import { useRouter } from "next/navigation";
import { ThemeProvider } from "next-themes";

export default function Providers({children}: {children: React.ReactNode}) {
  const router = useRouter();
  // 2. Wrap HeroUIProvider at the root of your app
  return (
    <HeroUIProvider navigate={router.push} className="h-full flex flex-col">
      <ThemeProvider attribute="class" defaultTheme="light">
        {children}
      </ThemeProvider>
    </HeroUIProvider>
  );
}

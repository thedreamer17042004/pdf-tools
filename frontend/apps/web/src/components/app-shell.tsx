"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useAuthStore } from "@/lib/auth";

const navLinks = [
  { href: "/workspace", label: "Workspace" },
  { href: "/editor", label: "Editor" },
  { href: "/tools", label: "PDF Tools" },
  { href: "/word", label: "Word" },
  { href: "/library", label: "Library" },
] as const;

type AppShellProps = {
  children: ReactNode;
  /** Optional toolbar row below main nav (page-specific controls). */
  toolbar?: ReactNode;
};

export function AppShell({ children, toolbar }: AppShellProps) {
  const pathname = usePathname();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  function isActive(href: string) {
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <header className="sticky top-0 z-30 border-b border-line bg-surface">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-6">
            <Link href="/" className="nam-dev-display text-lg text-ink">
              Nam-Dev
            </Link>
            <nav className="hidden items-center gap-1 sm:flex">
              {navLinks.map((link) => (
                <Link
                  key={link.href}
                  href={link.href}
                  className={`rounded-lg px-3 py-1.5 text-sm transition ${
                    isActive(link.href)
                      ? "bg-accent-soft font-medium text-accent"
                      : "text-muted hover:bg-background hover:text-ink"
                  }`}
                >
                  {link.label}
                </Link>
              ))}
            </nav>
          </div>
          <div className="flex items-center gap-3 text-sm">
            {user ? (
              <>
                <span className="hidden max-w-[180px] truncate text-muted md:inline">
                  {user.email}
                </span>
                <button
                  type="button"
                  onClick={logout}
                  className="rounded-lg border border-line px-3 py-1.5 text-ink transition hover:bg-background"
                >
                  Sign out
                </button>
              </>
            ) : (
              <Link
                href="/login"
                className="rounded-lg bg-cta px-3 py-1.5 font-medium text-white transition hover:opacity-90"
              >
                Sign in
              </Link>
            )}
          </div>
        </div>
        {toolbar ? (
          <div className="border-t border-line bg-surface">{toolbar}</div>
        ) : null}
      </header>
      {children}
    </div>
  );
}
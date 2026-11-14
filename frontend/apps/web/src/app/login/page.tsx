"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { login, register, useAuthStore } from "@/lib/auth";

export default function LoginPage() {
  const router = useRouter();
  const setUser = useAuthStore((s) => s.setUser);
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const auth =
        mode === "login"
          ? await login(email, password)
          : await register(email, password, displayName || undefined);
      setUser(auth);
      router.push("/library");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Auth failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AppShell>
      <main className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-6 py-16">
        <h1 className="text-2xl font-semibold text-ink">
          {mode === "login" ? "Sign in" : "Create account"}
        </h1>
        <p className="mt-2 text-sm text-muted">
          Access your document library and PDF tools.
        </p>
        <form onSubmit={onSubmit} className="mt-8 space-y-4">
          {mode === "register" ? (
            <input
              className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm outline-none focus:border-accent"
              placeholder="Display name"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          ) : null}
          <input
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm outline-none focus:border-accent"
            type="email"
            required
            placeholder="Email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <input
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm outline-none focus:border-accent"
            type="password"
            required
            minLength={8}
            placeholder="Password (min 8 characters)"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          {error ? <p className="text-sm text-danger">{error}</p> : null}
          <button
            type="submit"
            disabled={busy}
            className="w-full rounded-lg bg-accent py-2.5 text-sm font-semibold text-white transition hover:opacity-90 disabled:opacity-60"
          >
            {busy ? "Please wait…" : mode === "login" ? "Sign in" : "Register"}
          </button>
        </form>
        <button
          type="button"
          className="mt-4 text-sm text-accent hover:underline"
          onClick={() => setMode(mode === "login" ? "register" : "login")}
        >
          {mode === "login" ? "Need an account? Register" : "Have an account? Sign in"}
        </button>
        <Link href="/" className="mt-8 text-sm text-muted hover:text-ink">
          ← Back home
        </Link>
      </main>
    </AppShell>
  );
}
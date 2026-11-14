import Link from "next/link";

export default function HomePage() {
  return (
    <main className="flex min-h-screen flex-col">
      <header className="mx-auto flex w-full max-w-6xl items-center justify-between px-6 py-6">
        <p className="nam-dev-display text-2xl text-ink">Nam-Dev</p>
        <nav className="flex items-center gap-4 text-sm">
          <Link href="/library" className="text-muted hover:text-ink">
            Library
          </Link>
          <Link
            href="/login"
            className="rounded-lg border border-line bg-surface px-3 py-1.5 font-medium text-ink"
          >
            Sign in
          </Link>
        </nav>
      </header>

      <section className="mx-auto flex w-full max-w-6xl flex-1 flex-col justify-center px-6 pb-24 pt-8">
        <h1 className="nam-dev-display max-w-3xl text-5xl leading-tight text-ink md:text-6xl">
          Nam-Dev
        </h1>
        <p className="mt-5 max-w-xl text-lg text-muted">
          Professional PDF upload, viewing, editing, and processing — built on
          an independent PDF core engine.
        </p>
        <div className="mt-10 flex flex-wrap gap-3">
          <Link
            href="/library"
            className="rounded-lg bg-cta px-6 py-3 text-sm font-semibold text-white transition hover:opacity-90"
          >
            Open library
          </Link>
          <Link
            href="/tools"
            className="rounded-lg border border-line bg-surface px-6 py-3 text-sm font-semibold text-ink transition hover:bg-background"
          >
            Tools
          </Link>
          <Link
            href="/editor"
            className="rounded-lg border border-line bg-surface px-6 py-3 text-sm font-semibold text-ink transition hover:bg-background"
          >
            Editor
          </Link>
        </div>
      </section>

      <footer className="border-t border-line px-6 py-4 text-center text-xs text-muted">
        Nam-Dev PDF Platform
      </footer>
    </main>
  );
}
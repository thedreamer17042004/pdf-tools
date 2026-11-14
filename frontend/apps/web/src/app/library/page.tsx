"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { FileText, Trash2, Upload } from "lucide-react";
import { AppShell } from "@/components/app-shell";
import {
  deleteDocument,
  extractText,
  listMyDocuments,
  uploadDocument,
  type DocumentDto,
} from "@/lib/api";
import { useAuthStore } from "@/lib/auth";

export default function LibraryPage() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const [docs, setDocs] = useState<DocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [textPreview, setTextPreview] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const refresh = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      setDocs(await listMyDocuments(user.accessToken));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load library");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    if (!user) {
      setLoading(false);
      return;
    }
    void refresh();
  }, [user, refresh]);

  async function onUpload(file: File | undefined) {
    if (!file || !user) return;
    setBusy(true);
    setError(null);
    try {
      await uploadDocument(file, user.accessToken);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Upload failed");
    } finally {
      setBusy(false);
    }
  }

  async function onDelete(id: string) {
    if (!user || !confirm("Delete this document?")) return;
    setBusy(true);
    try {
      await deleteDocument(id, user.accessToken);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed");
    } finally {
      setBusy(false);
    }
  }

  async function onExtractText(id: string) {
    setBusy(true);
    setTextPreview(null);
    try {
      const result = await extractText(id);
      const preview = result.fullText.slice(0, 800);
      setTextPreview(preview || "(No text found)");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Extract failed");
    } finally {
      setBusy(false);
    }
  }

  if (!user) {
    return (
      <AppShell>
        <main className="mx-auto flex max-w-lg flex-1 flex-col justify-center px-6 py-16">
          <h1 className="text-2xl font-semibold text-ink">Your library</h1>
          <p className="mt-2 text-muted">Sign in to upload and manage your PDFs.</p>
          <Link
            href="/login"
            className="mt-6 inline-flex justify-center rounded-lg bg-cta px-5 py-2.5 text-sm font-semibold text-white"
          >
            Sign in
          </Link>
        </main>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-8">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold text-ink">Library</h1>
            <p className="mt-1 text-sm text-muted">
              {docs.length} document{docs.length === 1 ? "" : "s"} · {user.email}
            </p>
          </div>
          <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg bg-accent px-4 py-2.5 text-sm font-medium text-white transition hover:opacity-90">
            <Upload className="h-4 w-4" />
            {busy ? "Working…" : "Upload PDF"}
            <input
              ref={fileRef}
              type="file"
              accept="application/pdf,.pdf"
              className="hidden"
              disabled={busy}
              onChange={(e) => void onUpload(e.target.files?.[0])}
            />
          </label>
        </div>

        {error ? <p className="mt-4 text-sm text-danger">{error}</p> : null}

        {loading ? (
          <p className="mt-12 text-center text-sm text-muted">Loading documents…</p>
        ) : docs.length === 0 ? (
          <div className="mt-12 rounded-xl border border-dashed border-line bg-surface p-12 text-center">
            <FileText className="mx-auto h-10 w-10 text-muted" />
            <p className="mt-4 font-medium text-ink">No documents yet</p>
            <p className="mt-1 text-sm text-muted">Upload a PDF to get started.</p>
          </div>
        ) : (
          <ul className="mt-6 divide-y divide-line rounded-xl border border-line bg-surface">
            {docs.map((doc) => (
              <li
                key={doc.id}
                className="flex flex-wrap items-center justify-between gap-3 px-4 py-4"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-ink">{doc.fileName}</p>
                  <p className="text-xs text-muted">
                    {doc.pageCount} pages · {(doc.sizeBytes / 1024).toFixed(1)} KB ·{" "}
                    {new Date(doc.createdAtUtc).toLocaleDateString()}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <button
                    type="button"
                    className="rounded-lg border border-line px-3 py-1.5 text-ink hover:bg-background"
                    onClick={() => router.push(`/workspace?doc=${doc.id}`)}
                  >
                    Workspace
                  </button>
                  <button
                    type="button"
                    className="rounded-lg border border-line px-3 py-1.5 text-ink hover:bg-background"
                    onClick={() => router.push(`/editor?doc=${doc.id}`)}
                  >
                    Editor
                  </button>
                  <button
                    type="button"
                    className="rounded-lg border border-line px-3 py-1.5 text-muted hover:bg-background"
                    disabled={busy}
                    onClick={() => void onExtractText(doc.id)}
                  >
                    Text
                  </button>
                  <button
                    type="button"
                    className="rounded-lg border border-line p-2 text-danger hover:bg-background"
                    disabled={busy}
                    aria-label="Delete"
                    onClick={() => void onDelete(doc.id)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {textPreview ? (
          <section className="mt-8 rounded-xl border border-line bg-surface p-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-sm font-semibold text-ink">Extracted text preview</h2>
              <button
                type="button"
                className="text-xs text-muted hover:text-ink"
                onClick={() => setTextPreview(null)}
              >
                Close
              </button>
            </div>
            <pre className="mt-3 max-h-48 overflow-auto whitespace-pre-wrap text-xs text-muted">
              {textPreview}
            </pre>
          </section>
        ) : null}
      </main>
    </AppShell>
  );
}
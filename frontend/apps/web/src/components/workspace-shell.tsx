"use client";

import {
  ChevronLeft,
  ChevronRight,
  Download,
  Maximize2,
  Minus,
  Plus,
  Server,
} from "lucide-react";
import { AppShell } from "@/components/app-shell";
import { downloadUrl, serverRenderUrl } from "@/lib/api";
import { useOpenDocumentFromQuery } from "@/lib/open-document";
import { UploadDropzone } from "@/components/upload-dropzone";
import { PdfViewer } from "@/components/pdf-viewer";
import { useWorkspaceStore } from "@/store/workspace-store";

export function WorkspaceShell() {
  useOpenDocumentFromQuery();

  const document = useWorkspaceStore((s) => s.document);
  const localUrl = useWorkspaceStore((s) => s.localUrl);
  const currentPage = useWorkspaceStore((s) => s.currentPage);
  const zoom = useWorkspaceStore((s) => s.zoom);
  const showServerRender = useWorkspaceStore((s) => s.showServerRender);
  const setCurrentPage = useWorkspaceStore((s) => s.setCurrentPage);
  const setZoom = useWorkspaceStore((s) => s.setZoom);
  const setFitMode = useWorkspaceStore((s) => s.setFitMode);
  const toggleServerRender = useWorkspaceStore((s) => s.toggleServerRender);
  const reset = useWorkspaceStore((s) => s.reset);

  const pageCount = document?.pageCount ?? 1;

  const goTo = (page: number) => {
    const next = Math.min(pageCount, Math.max(1, page));
    setCurrentPage(next);
    const el = window.document.getElementById(`page-${next}`);
    el?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const toolbar = document ? (
    <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-center gap-2 px-4 py-2">
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => goTo(currentPage - 1)}
        aria-label="Previous page"
      >
        <ChevronLeft className="h-4 w-4" />
      </button>
      <span className="min-w-24 text-center text-sm text-muted">
        {currentPage} / {pageCount}
      </span>
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => goTo(currentPage + 1)}
        aria-label="Next page"
      >
        <ChevronRight className="h-4 w-4" />
      </button>
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => setZoom(Math.max(0.4, zoom - 0.1))}
      >
        <Minus className="h-4 w-4" />
      </button>
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => setFitMode("width")}
      >
        Fit width
      </button>
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => setFitMode("page")}
      >
        <Maximize2 className="mr-1 inline h-3.5 w-3.5" />
        Fit page
      </button>
      <button
        type="button"
        className="rounded-lg border border-line px-2 py-1 text-sm"
        onClick={() => setZoom(Math.min(3, zoom + 0.1))}
      >
        <Plus className="h-4 w-4" />
      </button>
      <button
        type="button"
        className={`inline-flex items-center gap-1 rounded-lg border px-2 py-1 text-sm ${
          showServerRender
            ? "border-accent bg-accent-soft text-accent"
            : "border-line"
        }`}
        onClick={toggleServerRender}
        title="Compare with PDFium server render"
      >
        <Server className="h-4 w-4" />
        Server PNG
      </button>
      <a
        href={downloadUrl(document.id)}
        className="inline-flex items-center gap-1 rounded-lg bg-accent px-3 py-1.5 text-sm font-medium text-white"
      >
        <Download className="h-4 w-4" />
        Download
      </a>
      <button
        type="button"
        className="rounded-lg border border-line px-3 py-1.5 text-sm"
        onClick={reset}
      >
        New
      </button>
    </div>
  ) : (
    <div className="mx-auto max-w-7xl px-4 py-2 text-center text-sm text-muted">
      PDF Workspace
    </div>
  );

  return (
    <AppShell toolbar={toolbar}>
      <main className="mx-auto flex w-full max-w-7xl flex-1 gap-4 px-4 py-4">
        {!document || !localUrl ? (
          <div className="w-full py-16">
            <UploadDropzone />
          </div>
        ) : (
          <>
            <aside className="hidden w-48 shrink-0 lg:block">
              <div className="sticky top-32 space-y-2 rounded-xl border border-line bg-surface p-3">
                <p className="text-xs font-medium uppercase tracking-wide text-muted">
                  Pages
                </p>
                <div className="max-h-[70vh] space-y-1 overflow-auto">
                  {Array.from({ length: pageCount }, (_, i) => i + 1).map((p) => (
                    <button
                      key={p}
                      type="button"
                      onClick={() => goTo(p)}
                      className={`block w-full rounded-lg px-2 py-1.5 text-left text-sm ${
                        p === currentPage
                          ? "bg-accent-soft text-accent"
                          : "hover:bg-background"
                      }`}
                    >
                      Page {p}
                    </button>
                  ))}
                </div>
                <div className="border-t border-line pt-3 text-xs text-muted">
                  <p className="truncate font-medium text-ink">{document.fileName}</p>
                  <p>{document.pageCount} pages</p>
                  <p>{(document.sizeBytes / 1024).toFixed(1)} KB</p>
                </div>
              </div>
            </aside>

            <section
              id="nam-dev-viewer-scroll"
              className="min-h-[70vh] flex-1 overflow-auto rounded-xl border border-line bg-slate-100"
            >
              <PdfViewer url={localUrl} pageCountHint={document.pageCount} />
            </section>

            {showServerRender ? (
              <aside className="hidden w-64 shrink-0 xl:block">
                <div className="sticky top-32 rounded-xl border border-line bg-surface p-3">
                  <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">
                    PDFium render
                  </p>
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={serverRenderUrl(document.id, currentPage, 120)}
                    alt={`Server render page ${currentPage}`}
                    className="w-full rounded-lg border border-line bg-white"
                  />
                </div>
              </aside>
            ) : null}
          </>
        )}
      </main>
    </AppShell>
  );
}
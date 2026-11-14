"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Download,
  Eraser,
  Hand,
  Highlighter,
  MousePointer2,
  Pencil,
  Save,
  Square,
  Type,
  Underline,
  Upload,
} from "lucide-react";
import { AppShell } from "@/components/app-shell";
import { UploadDropzone } from "@/components/upload-dropzone";
import { PdfViewer } from "@/components/pdf-viewer";
import type { EditorToolId } from "@/components/annotation-layer";
import { useOpenDocumentFromQuery } from "@/lib/open-document";
import { useWorkspaceStore } from "@/store/workspace-store";
import {
  exportXfdf,
  importXfdf,
  listAnnotations,
  saveAnnotations,
  type AnnotationDto,
  type AnnotationUpsert,
} from "@/lib/annotations-api";

const tools = [
  { id: "select", label: "Select", icon: MousePointer2 },
  { id: "hand", label: "Hand", icon: Hand },
  { id: "text", label: "Text", icon: Type },
  { id: "highlight", label: "Highlight", icon: Highlighter },
  { id: "underline", label: "Underline", icon: Underline },
  { id: "pencil", label: "Pencil", icon: Pencil },
  { id: "shapes", label: "Shapes", icon: Square },
  { id: "eraser", label: "Eraser", icon: Eraser },
] as const satisfies ReadonlyArray<{ id: EditorToolId; label: string; icon: typeof Save }>;

export function EditorShell() {
  useOpenDocumentFromQuery();

  const document = useWorkspaceStore((s) => s.document);
  const localUrl = useWorkspaceStore((s) => s.localUrl);
  const currentPage = useWorkspaceStore((s) => s.currentPage);
  const [activeTool, setActiveTool] = useState<EditorToolId>("highlight");
  const [strokeColor, setStrokeColor] = useState("#0f766e");
  const [annotations, setAnnotations] = useState<AnnotationDto[]>([]);
  const [draft, setDraft] = useState<AnnotationUpsert[] | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const working = draft ?? annotations.map(toUpsert);

  useEffect(() => {
    if (!document) {
      setAnnotations([]);
      setDraft(null);
      return;
    }
    let cancelled = false;
    listAnnotations(document.id)
      .then((items) => {
        if (!cancelled) {
          setAnnotations(items);
          setDraft(null);
        }
      })
      .catch((err) => setStatus(err instanceof Error ? err.message : "Load failed"));
    return () => {
      cancelled = true;
    };
  }, [document?.id]);

  const onChange = useCallback((next: AnnotationUpsert[]) => {
    setDraft(next);
  }, []);

  const displayAnns: AnnotationDto[] = useMemo(
    () =>
      working.map((a) => ({
        id: a.id || crypto.randomUUID(),
        documentId: document?.id || "",
        type: a.type,
        pageIndex: a.pageIndex,
        x: a.x,
        y: a.y,
        width: a.width,
        height: a.height,
        color: a.color,
        opacity: a.opacity ?? 1,
        contents: a.contents,
        author: a.author,
        geometryJson: a.geometryJson,
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      })),
    [working, document?.id],
  );

  async function onSave() {
    if (!document) return;
    setBusy(true);
    setStatus(null);
    try {
      const saved = await saveAnnotations(document.id, working);
      setAnnotations(saved);
      setDraft(null);
      setStatus(`Saved ${saved.length} annotation(s).`);
    } catch (e) {
      setStatus(e instanceof Error ? e.message : "Save failed");
    } finally {
      setBusy(false);
    }
  }

  async function onExportXfdf() {
    if (!document) return;
    const xml = await exportXfdf(document.id);
    const blob = new Blob([xml], { type: "application/vnd.adobe.xfdf" });
    const url = URL.createObjectURL(blob);
    const a = window.document.createElement("a");
    a.href = url;
    a.download = `${document.fileName.replace(/\.pdf$/i, "")}.xfdf`;
    a.click();
    URL.revokeObjectURL(url);
  }

  async function onImportXfdf(file: File | undefined) {
    if (!document || !file) return;
    setBusy(true);
    try {
      const xml = await file.text();
      const items = await importXfdf(document.id, xml, true);
      setAnnotations(items);
      setDraft(null);
      setStatus(`Imported ${items.length} annotation(s) from XFDF.`);
    } catch (e) {
      setStatus(e instanceof Error ? e.message : "Import failed");
    } finally {
      setBusy(false);
    }
  }

  const toolbar = (
    <div className="mx-auto flex max-w-[1400px] flex-wrap items-center justify-between gap-2 px-4 py-2">
      <span className="text-sm text-muted">PDF Editor</span>
      <div className="flex flex-wrap items-center gap-1">
        {tools.map((tool) => {
          const Icon = tool.icon;
          const active = activeTool === tool.id;
          return (
            <button
              key={tool.id}
              type="button"
              title={tool.label}
              data-testid={`tool-${tool.id}`}
              onClick={() => setActiveTool(tool.id)}
              className={`inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs ${
                active ? "bg-accent-soft text-accent" : "text-ink hover:bg-background"
              }`}
            >
              <Icon className="h-3.5 w-3.5" />
              <span className="hidden sm:inline">{tool.label}</span>
            </button>
          );
        })}
        <input
          type="color"
          value={strokeColor}
          onChange={(e) => setStrokeColor(e.target.value)}
          className="ml-2 h-8 w-8 cursor-pointer rounded-lg border border-line bg-transparent"
          title="Stroke color"
        />
        <button
          type="button"
          data-testid="save-annotations"
          disabled={!document || busy}
          onClick={() => void onSave()}
          className="ml-2 inline-flex items-center gap-1 rounded-lg bg-accent px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50"
        >
          <Save className="h-3.5 w-3.5" />
          Save
        </button>
        <button
          type="button"
          data-testid="export-xfdf"
          disabled={!document}
          onClick={() => void onExportXfdf()}
          className="inline-flex items-center gap-1 rounded-lg border border-line px-3 py-1.5 text-xs"
        >
          <Download className="h-3.5 w-3.5" />
          XFDF
        </button>
        <label className="inline-flex cursor-pointer items-center gap-1 rounded-lg border border-line px-3 py-1.5 text-xs">
          <Upload className="h-3.5 w-3.5" />
          Import
          <input
            type="file"
            accept=".xfdf,application/vnd.adobe.xfdf,text/xml"
            className="hidden"
            data-testid="import-xfdf"
            onChange={(e) => void onImportXfdf(e.target.files?.[0])}
          />
        </label>
      </div>
    </div>
  );

  return (
    <AppShell toolbar={toolbar}>
      <div className="mx-auto flex w-full max-w-[1400px] flex-1">
        <aside className="hidden w-48 shrink-0 border-r border-line bg-surface p-3 md:block">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">Pages</p>
          <div className="mt-2 space-y-1">
            {document
              ? Array.from({ length: document.pageCount }, (_, i) => i + 1).map((p) => (
                  <div
                    key={p}
                    className={`rounded-lg px-2 py-1.5 text-sm ${
                      p === currentPage ? "bg-accent-soft text-accent" : "text-ink"
                    }`}
                  >
                    Page {p}
                  </div>
                ))
              : (
                <p className="text-xs text-muted">Open a PDF to begin.</p>
              )}
          </div>
        </aside>

        <section id="nam-dev-viewer-scroll" className="relative min-h-[70vh] flex-1 overflow-auto bg-slate-50">
          {!document || !localUrl ? (
            <div className="p-8">
              <UploadDropzone />
            </div>
          ) : (
            <PdfViewer
              url={localUrl}
              pageCountHint={document.pageCount}
              annotationMode
              annotations={displayAnns}
              activeTool={activeTool}
              strokeColor={strokeColor}
              onAnnotationsChange={onChange}
            />
          )}
        </section>

        <aside className="hidden w-56 shrink-0 border-l border-line bg-surface p-3 lg:block">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">Properties</p>
          <dl className="mt-3 space-y-2 text-sm">
            <div>
              <dt className="text-muted">Active tool</dt>
              <dd className="font-medium text-ink" data-testid="active-tool">
                {activeTool}
              </dd>
            </div>
            <div>
              <dt className="text-muted">Annotations</dt>
              <dd className="font-medium text-ink" data-testid="annotation-count">
                {working.length}
              </dd>
            </div>
            <div>
              <dt className="text-muted">Document</dt>
              <dd className="truncate font-medium text-ink">{document?.fileName ?? "—"}</dd>
            </div>
          </dl>
          {status ? (
            <p className="mt-4 text-xs text-accent" data-testid="editor-status">
              {status}
            </p>
          ) : null}
        </aside>
      </div>

      <footer className="border-t border-line bg-surface px-4 py-2 text-xs text-muted">
        {document
          ? `${document.fileName} · ${document.pageCount} pages · page ${currentPage} · ${working.length} annotations`
          : "No document loaded"}
      </footer>
    </AppShell>
  );
}

function toUpsert(a: AnnotationDto): AnnotationUpsert {
  return {
    id: a.id,
    type: a.type,
    pageIndex: a.pageIndex,
    x: a.x,
    y: a.y,
    width: a.width,
    height: a.height,
    color: a.color ?? undefined,
    opacity: a.opacity,
    contents: a.contents ?? undefined,
    author: a.author ?? undefined,
    geometryJson: a.geometryJson ?? undefined,
  };
}
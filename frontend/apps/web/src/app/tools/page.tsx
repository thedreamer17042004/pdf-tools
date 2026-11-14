"use client";

import Link from "next/link";
import { useState } from "react";
import { AppShell } from "@/components/app-shell";
import { useAuthStore } from "@/lib/auth";
import {
  coreCompare,
  coreHtmlToPdf,
  coreRepair,
  coreSignStamp,
  coreToMarkdown,
  downloadBlob,
  enqueueCompress,
  enqueueDeletePages,
  enqueueExtractPages,
  enqueueMerge,
  enqueueOrganize,
  enqueuePageNumbers,
  enqueuePdfToImages,
  enqueueProtect,
  enqueueReverse,
  enqueueRotate,
  enqueueSplit,
  enqueueUnlock,
  enqueueWatermark,
  imagesToPdf,
  jobDownloadUrl,
  pollJob,
  uploadMany,
  type JobDto,
} from "@/lib/tools-api";

function parsePageIndexes(raw: string): number[] | null {
  const indexes = raw
    .split(/[,\s]+/)
    .map((s) => s.trim())
    .filter(Boolean)
    .map((s) => Number.parseInt(s, 10));
  if (indexes.length === 0 || indexes.some((n) => Number.isNaN(n) || n < 0)) {
    return null;
  }
  return indexes;
}

export default function ToolsPage() {
  const user = useAuthStore((s) => s.user);
  const [message, setMessage] = useState<string | null>(null);
  const [job, setJob] = useState<JobDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [organizeOrder, setOrganizeOrder] = useState("1,0");
  const [extractIndexes, setExtractIndexes] = useState("0");
  const [deleteIndexes, setDeleteIndexes] = useState("0");
  const [pageNumberFormat, setPageNumberFormat] = useState("");
  const [unlockPassword, setUnlockPassword] = useState("");
  const [watermarkText, setWatermarkText] = useState("Nam-Dev");
  const [protectPassword, setProtectPassword] = useState("secret");
  const [signerName, setSignerName] = useState("Nam-Dev");
  const [htmlSource, setHtmlSource] = useState("<h1>Hello</h1><p>From Nam-Dev HTML→PDF</p>");

  if (!user) {
    return (
      <AppShell>
        <main className="mx-auto flex max-w-lg flex-1 flex-col justify-center px-6 py-16">
          <h1 className="text-2xl font-semibold text-ink">PDF Tools</h1>
          <p className="mt-3 text-muted">
            Sign in to use the full PDF tool suite.
          </p>
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

  async function runMerge(files: FileList | null) {
    if (!files || files.length < 2) {
      setMessage("Select at least two PDFs to merge.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const docs = await uploadMany(Array.from(files));
      const queued = await enqueueMerge(docs.map((d) => d.id));
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Merge complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Merge failed");
    } finally {
      setBusy(false);
    }
  }

  async function runSplit(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueSplit(doc.id);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Split complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Split failed");
    } finally {
      setBusy(false);
    }
  }

  async function runRotate(files: FileList | null, degrees: number) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueRotate(doc.id, degrees);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? `Rotated ${degrees}°.` : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Rotate failed");
    } finally {
      setBusy(false);
    }
  }

  async function runCompress(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueCompress(doc.id, true);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Compress complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Compress failed");
    } finally {
      setBusy(false);
    }
  }

  async function runOrganize(files: FileList | null) {
    if (!files?.[0]) return;
    const pageOrder = parsePageIndexes(organizeOrder);
    if (!pageOrder) {
      setMessage("Enter a valid 0-based page order, e.g. 1,0 to reverse a 2-page PDF.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueOrganize(doc.id, pageOrder);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Organize complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Organize failed");
    } finally {
      setBusy(false);
    }
  }

  async function runWatermark(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueWatermark(doc.id, watermarkText || "Nam-Dev");
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Watermark complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Watermark failed");
    } finally {
      setBusy(false);
    }
  }

  async function runProtect(files: FileList | null) {
    if (!files?.[0]) return;
    if (protectPassword.length < 4) {
      setMessage("Password must be at least 4 characters.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueProtect(doc.id, protectPassword);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Protect complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Protect failed");
    } finally {
      setBusy(false);
    }
  }

  async function runExtractPages(files: FileList | null) {
    if (!files?.[0]) return;
    const pageIndexes = parsePageIndexes(extractIndexes);
    if (!pageIndexes) {
      setMessage("Enter valid 0-based page indexes, e.g. 0,1.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueExtractPages(doc.id, pageIndexes);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Extract complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Extract failed");
    } finally {
      setBusy(false);
    }
  }

  async function runReverse(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueReverse(doc.id);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Reverse complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Reverse failed");
    } finally {
      setBusy(false);
    }
  }

  async function runDeletePages(files: FileList | null) {
    if (!files?.[0]) return;
    const pageIndexes = parsePageIndexes(deleteIndexes);
    if (!pageIndexes) {
      setMessage("Enter valid 0-based page indexes to delete, e.g. 0.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueDeletePages(doc.id, pageIndexes);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Delete pages complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Delete pages failed");
    } finally {
      setBusy(false);
    }
  }

  async function runPageNumbers(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const format = pageNumberFormat.trim() || "{n} / {total}";
      const queued = await enqueuePageNumbers(doc.id, format);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Page numbers added." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Page numbers failed");
    } finally {
      setBusy(false);
    }
  }

  async function runUnlock(files: FileList | null) {
    if (!files?.[0]) return;
    if (!unlockPassword) {
      setMessage("Enter the PDF password to unlock.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueueUnlock(doc.id, unlockPassword);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "Unlock complete." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Unlock failed");
    } finally {
      setBusy(false);
    }
  }

  async function runPdfToImages(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const [doc] = await uploadMany([files[0]]);
      const queued = await enqueuePdfToImages(doc.id);
      const done = await pollJob(queued.id);
      setJob(done);
      setMessage(done.status === "Succeeded" ? "PDF → images complete (ZIP)." : (done.error ?? "Failed"));
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "PDF to images failed");
    } finally {
      setBusy(false);
    }
  }

  async function runImagesToPdf(files: FileList | null) {
    if (!files || files.length === 0) return;
    setBusy(true);
    setMessage(null);
    try {
      const blob = await imagesToPdf(Array.from(files));
      downloadBlob(blob, "images.pdf");
      setMessage("Images converted to PDF.");
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Image conversion failed");
    } finally {
      setBusy(false);
    }
  }

  async function runRepair(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const blob = await coreRepair(files[0]);
      downloadBlob(blob, "repaired.pdf");
      setMessage("Repair complete.");
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Repair failed");
    } finally {
      setBusy(false);
    }
  }

  async function runToMarkdown(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const md = await coreToMarkdown(files[0]);
      downloadBlob(new Blob([md], { type: "text/markdown" }), "export.md");
      setMessage("PDF → Markdown complete.");
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Markdown export failed");
    } finally {
      setBusy(false);
    }
  }

  async function runCompare(files: FileList | null) {
    if (!files || files.length < 2) {
      setMessage("Select two PDFs to compare.");
      return;
    }
    setBusy(true);
    setMessage(null);
    try {
      const result = (await coreCompare(files[0], files[1])) as { summary?: string };
      setMessage(result.summary ?? JSON.stringify(result));
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Compare failed");
    } finally {
      setBusy(false);
    }
  }

  async function runHtmlToPdf() {
    setBusy(true);
    setMessage(null);
    try {
      const blob = await coreHtmlToPdf(htmlSource, "Nam-Dev");
      downloadBlob(blob, "from-html.pdf");
      setMessage("HTML → PDF complete.");
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "HTML to PDF failed");
    } finally {
      setBusy(false);
    }
  }

  async function runSignStamp(files: FileList | null) {
    if (!files?.[0]) return;
    setBusy(true);
    setMessage(null);
    try {
      const blob = await coreSignStamp(files[0], {
        pageIndex: 0,
        xPoints: 72,
        yPoints: 72,
        signerName,
      });
      downloadBlob(blob, "signed.pdf");
      setMessage("Signature stamp applied (visual, not PAdES).");
      setJob(null);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Sign stamp failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AppShell>
      <main className="mx-auto w-full max-w-4xl flex-1 px-4 py-8">
        <div className="mb-8">
          <h1 className="text-2xl font-semibold text-ink">PDF Tools</h1>
          <p className="mt-1 text-sm text-muted">
            Core suite aligned with iLovePDF-style tools — plus repair, compare, markdown,
            HTML→PDF, and visual sign stamp.
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <ToolCard title="Merge PDF" description="Combine two or more PDFs into one file.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDFs"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                multiple
                className="hidden"
                data-testid="tool-merge"
                disabled={busy}
                onChange={(e) => void runMerge(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Split PDF" description="Split every page into separate files (ZIP).">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-split"
                disabled={busy}
                onChange={(e) => void runSplit(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Rotate PDF" description="Rotate all pages 90° clockwise.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-rotate"
                disabled={busy}
                onChange={(e) => void runRotate(e.target.files, 90)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Compress PDF" description="Rebuild streams and strip metadata to shrink size.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-compress"
                disabled={busy}
                onChange={(e) => void runCompress(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard
            title="Organize pages"
            description="Reorder or drop pages. Use 0-based indexes (e.g. 1,0 reverses a 2-page file)."
          >
            <input
              value={organizeOrder}
              onChange={(e) => setOrganizeOrder(e.target.value)}
              data-testid="organize-order"
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="1,0"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-organize"
                disabled={busy}
                onChange={(e) => void runOrganize(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Watermark" description="Stamp diagonal text across every page.">
            <input
              value={watermarkText}
              onChange={(e) => setWatermarkText(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Nam-Dev"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-watermark"
                disabled={busy}
                onChange={(e) => void runWatermark(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Protect PDF" description="Encrypt with a user password (min 4 chars).">
            <input
              type="password"
              value={protectPassword}
              onChange={(e) => setProtectPassword(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Password"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-protect"
                disabled={busy}
                onChange={(e) => void runProtect(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Images → PDF" description="Combine PNG/JPEG images into a single PDF.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose images"}
              <input
                type="file"
                accept="image/png,image/jpeg,image/jpg,.png,.jpg,.jpeg"
                multiple
                className="hidden"
                data-testid="tool-images"
                disabled={busy}
                onChange={(e) => void runImagesToPdf(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard
            title="Extract pages"
            description="Pull specific pages into a new PDF. Use 0-based indexes (e.g. 0,1)."
          >
            <input
              value={extractIndexes}
              onChange={(e) => setExtractIndexes(e.target.value)}
              data-testid="extract-indexes"
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="0,1"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-extract"
                disabled={busy}
                onChange={(e) => void runExtractPages(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Reverse pages" description="Flip page order end-to-end.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-reverse"
                disabled={busy}
                onChange={(e) => void runReverse(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard
            title="Delete pages"
            description="Remove pages by 0-based index (e.g. 0 removes the first page)."
          >
            <input
              value={deleteIndexes}
              onChange={(e) => setDeleteIndexes(e.target.value)}
              data-testid="delete-indexes"
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="0"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-delete-pages"
                disabled={busy}
                onChange={(e) => void runDeletePages(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard
            title="Page numbers"
            description="Stamp page numbers on every page. Default format: {n} / {total}."
          >
            <input
              value={pageNumberFormat}
              onChange={(e) => setPageNumberFormat(e.target.value)}
              data-testid="page-number-format"
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="{n} / {total}"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-page-numbers"
                disabled={busy}
                onChange={(e) => void runPageNumbers(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Unlock PDF" description="Remove password protection using the user password.">
            <input
              type="password"
              value={unlockPassword}
              onChange={(e) => setUnlockPassword(e.target.value)}
              data-testid="unlock-password"
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Password"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-unlock"
                disabled={busy}
                onChange={(e) => void runUnlock(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="PDF → Images" description="Rasterize each page to PNG and download as ZIP.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-pdf-to-images"
                disabled={busy}
                onChange={(e) => void runPdfToImages(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Repair PDF" description="Rebuild PDF by re-importing pages (best-effort).">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-repair"
                disabled={busy}
                onChange={(e) => void runRepair(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="Compare PDF" description="Compare page count and extracted text of two PDFs.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose 2 PDFs"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                multiple
                className="hidden"
                data-testid="tool-compare"
                disabled={busy}
                onChange={(e) => void runCompare(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="PDF → Markdown" description="Export extractable text as Markdown.">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-to-markdown"
                disabled={busy}
                onChange={(e) => void runToMarkdown(e.target.files)}
              />
            </label>
          </ToolCard>

          <ToolCard title="HTML → PDF" description="Convert simple HTML (headings, paragraphs, lists) to PDF.">
            <textarea
              value={htmlSource}
              onChange={(e) => setHtmlSource(e.target.value)}
              className="mt-3 h-24 w-full rounded-lg border border-line bg-background px-3 py-2 font-mono text-xs"
            />
            <button
              type="button"
              disabled={busy}
              data-testid="tool-html-to-pdf"
              className="mt-3 inline-flex rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-60"
              onClick={() => void runHtmlToPdf()}
            >
              {busy ? "Working…" : "Convert"}
            </button>
          </ToolCard>

          <ToolCard title="Sign stamp" description="Visual signature box (not cryptographic / PAdES).">
            <input
              value={signerName}
              onChange={(e) => setSignerName(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Signer name"
            />
            <label className="mt-3 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose PDF"}
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                data-testid="tool-sign-stamp"
                disabled={busy}
                onChange={(e) => void runSignStamp(e.target.files)}
              />
            </label>
          </ToolCard>
        </div>

        {message ? (
          <p className="mt-8 text-sm text-ink" data-testid="tools-message">
            {message}
          </p>
        ) : null}
        {job?.hasResult ? (
          <a
            className="mt-4 inline-flex rounded-lg border border-line bg-surface px-4 py-2 text-sm font-medium text-accent"
            href={jobDownloadUrl(job.id)}
            data-testid="job-download"
            onClick={async (e) => {
              e.preventDefault();
              const res = await fetch(jobDownloadUrl(job.id), {
                headers: { Authorization: `Bearer ${user.accessToken}` },
              });
              const blob = await res.blob();
              const url = URL.createObjectURL(blob);
              const a = document.createElement("a");
              a.href = url;
              a.download = job.resultFileName ?? "result.bin";
              a.click();
              URL.revokeObjectURL(url);
            }}
          >
            Download result ({job.resultFileName})
          </a>
        ) : null}
      </main>
    </AppShell>
  );
}

function ToolCard({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold text-ink">{title}</h2>
      <p className="mt-2 text-sm text-muted">{description}</p>
      {children}
    </section>
  );
}
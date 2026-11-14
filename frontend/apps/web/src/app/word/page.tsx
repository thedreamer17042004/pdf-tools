"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { useAuthStore } from "@/lib/auth";
import {
  downloadBlob,
  wordAppend,
  wordCreate,
  wordExtractText,
  wordInfo,
  wordListMergeFields,
  wordMergeFields,
  wordReplaceText,
  wordToPdf,
  type WordInfo,
} from "@/lib/word-api";

export default function WordToolsPage() {
  const user = useAuthStore((s) => s.user);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [createText, setCreateText] = useState(
    "Dear {{FullName}},\n\nYour appointment is on «Date».\n\nRegards,\nNam-Dev",
  );
  const [createTitle, setCreateTitle] = useState("Letter template");
  const [mergeJson, setMergeJson] = useState(
    '{\n  "FullName": "Ada Lovelace",\n  "Date": "2026-11-14"\n}',
  );
  const [findText, setFindText] = useState("Nam-Dev");
  const [replaceText, setReplaceText] = useState("Nam-Dev Docs");
  const [lastInfo, setLastInfo] = useState<WordInfo | null>(null);
  const [extracted, setExtracted] = useState<string | null>(null);
  const [detectedFields, setDetectedFields] = useState<string[]>([]);

  const acceptDocx =
    ".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document";

  const fieldHint = useMemo(
    () =>
      detectedFields.length > 0
        ? `Detected fields: ${detectedFields.join(", ")}`
        : "Supports MERGEFIELD, «Name», {{Name}}",
    [detectedFields],
  );

  if (!user) {
    return (
      <AppShell>
        <main className="mx-auto flex max-w-lg flex-1 flex-col justify-center px-6 py-16">
          <h1 className="text-2xl font-semibold text-ink">Word Tools</h1>
          <p className="mt-3 text-muted">
            Sign in to merge fields, edit DOCX, and convert Word to PDF.
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

  async function run<T>(fn: () => Promise<T>, okMessage: string) {
    setBusy(true);
    setMessage(null);
    try {
      await fn();
      setMessage(okMessage);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Operation failed");
    } finally {
      setBusy(false);
    }
  }

  function parseMergeValues(): Record<string, string> {
    const parsed = JSON.parse(mergeJson) as Record<string, unknown>;
    const values: Record<string, string> = {};
    for (const [k, v] of Object.entries(parsed)) {
      values[k] = String(v ?? "");
    }
    return values;
  }

  return (
    <AppShell>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        <div className="mb-8">
          <p className="text-xs font-medium uppercase tracking-wide text-accent">
            Open XML · Aspose.Words-inspired
          </p>
          <h1 className="mt-1 text-2xl font-semibold text-ink">Word Tools</h1>
          <p className="mt-1 text-sm text-muted">
            Create DOCX, mail-merge fields, replace text, append documents, and convert to PDF.
          </p>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <Card title="Create DOCX" description="Plain text → Word template with placeholders.">
            <input
              value={createTitle}
              onChange={(e) => setCreateTitle(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Title"
            />
            <textarea
              value={createText}
              onChange={(e) => setCreateText(e.target.value)}
              className="mt-3 h-36 w-full rounded-lg border border-line bg-background px-3 py-2 font-mono text-xs"
              data-testid="word-create-text"
            />
            <button
              type="button"
              disabled={busy}
              data-testid="word-create"
              className="mt-3 inline-flex rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
              onClick={() =>
                void run(async () => {
                  const blob = await wordCreate(createText, createTitle);
                  downloadBlob(blob, "template.docx");
                }, "DOCX created.")
              }
            >
              {busy ? "Working…" : "Create & download"}
            </button>
          </Card>

          <Card title="Document info" description="Read metadata and merge-field names.">
            <p className="mt-2 text-xs text-muted">{fieldHint}</p>
            <FileButton
              accept={acceptDocx}
              busy={busy}
              testId="word-info"
              label="Choose DOCX"
              onFile={(file) =>
                void run(async () => {
                  const info = await wordInfo(file);
                  setLastInfo(info);
                  setDetectedFields(info.mergeFields ?? []);
                }, "Info loaded.")
              }
            />
            {lastInfo ? (
              <dl className="mt-4 space-y-1 text-sm text-ink">
                <div>
                  <dt className="inline text-muted">Title: </dt>
                  <dd className="inline">{lastInfo.title || "—"}</dd>
                </div>
                <div>
                  <dt className="inline text-muted">Author: </dt>
                  <dd className="inline">{lastInfo.author || "—"}</dd>
                </div>
                <div>
                  <dt className="inline text-muted">Paragraphs: </dt>
                  <dd className="inline">{lastInfo.paragraphCount}</dd>
                </div>
                <div>
                  <dt className="inline text-muted">Fields: </dt>
                  <dd className="inline">
                    {(lastInfo.mergeFields ?? []).join(", ") || "none"}
                  </dd>
                </div>
              </dl>
            ) : null}
          </Card>

          <Card
            title="Merge fields"
            description="Fill {{Name}} / «Name» / MERGEFIELD from JSON values."
          >
            <textarea
              value={mergeJson}
              onChange={(e) => setMergeJson(e.target.value)}
              className="mt-3 h-32 w-full rounded-lg border border-line bg-background px-3 py-2 font-mono text-xs"
              data-testid="word-merge-json"
            />
            <div className="mt-3 flex flex-wrap gap-2">
              <FileButton
                accept={acceptDocx}
                busy={busy}
                testId="word-list-fields"
                label="List fields"
                onFile={(file) =>
                  void run(async () => {
                    const fields = await wordListMergeFields(file);
                    setDetectedFields(fields);
                    setMessage(`Found ${fields.length} field(s).`);
                  }, "Fields listed.")
                }
              />
              <FileButton
                accept={acceptDocx}
                busy={busy}
                testId="word-merge"
                label="Merge & download"
                onFile={(file) =>
                  void run(async () => {
                    const values = parseMergeValues();
                    const blob = await wordMergeFields(file, values);
                    downloadBlob(blob, "merged.docx");
                  }, "Merge complete.")
                }
              />
            </div>
          </Card>

          <Card title="Word → PDF" description="Convert DOCX to PDF via Nam-Dev Open XML → PdfSharp engine.">
            <FileButton
              accept={acceptDocx}
              busy={busy}
              testId="word-to-pdf"
              label="Choose DOCX"
              onFile={(file) =>
                void run(async () => {
                  const blob = await wordToPdf(file);
                  downloadBlob(blob, "from-word.pdf");
                }, "PDF ready.")
              }
            />
          </Card>

          <Card title="Replace text" description="Find & replace across the document body.">
            <input
              value={findText}
              onChange={(e) => setFindText(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Find"
            />
            <input
              value={replaceText}
              onChange={(e) => setReplaceText(e.target.value)}
              className="mt-3 w-full rounded-lg border border-line bg-background px-3 py-2 text-sm"
              placeholder="Replace"
            />
            <FileButton
              accept={acceptDocx}
              busy={busy}
              testId="word-replace"
              label="Choose DOCX"
              onFile={(file) =>
                void run(async () => {
                  const blob = await wordReplaceText(file, findText, replaceText);
                  downloadBlob(blob, "replaced.docx");
                }, "Replace complete.")
              }
            />
          </Card>

          <Card title="Append DOCX" description="Concatenate two Word files (page break between).">
            <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
              {busy ? "Working…" : "Choose 2 DOCX"}
              <input
                type="file"
                accept={acceptDocx}
                multiple
                className="hidden"
                data-testid="word-append"
                disabled={busy}
                onChange={(e) => {
                  const files = e.target.files;
                  if (!files || files.length < 2) {
                    setMessage("Select two DOCX files.");
                    return;
                  }
                  void run(async () => {
                    const blob = await wordAppend(files[0], files[1]);
                    downloadBlob(blob, "appended.docx");
                  }, "Append complete.");
                }}
              />
            </label>
          </Card>

          <Card title="Extract text" description="Pull plain text from a DOCX.">
            <FileButton
              accept={acceptDocx}
              busy={busy}
              testId="word-extract"
              label="Choose DOCX"
              onFile={(file) =>
                void run(async () => {
                  const text = await wordExtractText(file);
                  setExtracted(text);
                }, "Text extracted.")
              }
            />
            {extracted ? (
              <pre className="mt-3 max-h-40 overflow-auto rounded-lg border border-line bg-background p-3 text-xs text-ink whitespace-pre-wrap">
                {extracted}
              </pre>
            ) : null}
          </Card>
        </div>

        {message ? (
          <p className="mt-8 text-sm text-ink" data-testid="word-message">
            {message}
          </p>
        ) : null}
      </main>
    </AppShell>
  );
}

function Card({
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

function FileButton({
  accept,
  busy,
  label,
  testId,
  onFile,
}: {
  accept: string;
  busy: boolean;
  label: string;
  testId: string;
  onFile: (file: File) => void;
}) {
  return (
    <label className="mt-4 inline-flex cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90">
      {busy ? "Working…" : label}
      <input
        type="file"
        accept={accept}
        className="hidden"
        data-testid={testId}
        disabled={busy}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onFile(file);
          e.target.value = "";
        }}
      />
    </label>
  );
}

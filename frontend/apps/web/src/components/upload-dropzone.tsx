"use client";

import { useCallback, useState } from "react";
import { Upload } from "lucide-react";
import { uploadDocument } from "@/lib/api";
import { useAuthStore } from "@/lib/auth";
import { useWorkspaceStore } from "@/store/workspace-store";

export function UploadDropzone() {
  const [dragging, setDragging] = useState(false);
  const user = useAuthStore((s) => s.user);
  const setDocument = useWorkspaceStore((s) => s.setDocument);
  const setStatus = useWorkspaceStore((s) => s.setStatus);
  const status = useWorkspaceStore((s) => s.status);
  const error = useWorkspaceStore((s) => s.error);

  const onFile = useCallback(
    async (file: File | undefined) => {
      if (!file) return;
      if (!file.name.toLowerCase().endsWith(".pdf") && file.type !== "application/pdf") {
        setStatus("error", "Please choose a PDF file.");
        return;
      }

      setStatus("uploading");
      try {
        const localUrl = URL.createObjectURL(file);
        const doc = await uploadDocument(file, user?.accessToken);
        setDocument(doc, localUrl);
      } catch (err) {
        setStatus("error", err instanceof Error ? err.message : "Upload failed");
      }
    },
    [setDocument, setStatus, user?.accessToken],
  );

  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        void onFile(e.dataTransfer.files?.[0]);
      }}
      className={`flex min-h-[280px] flex-col items-center justify-center rounded-xl border border-dashed px-6 text-center transition ${
        dragging
          ? "border-accent bg-accent-soft"
          : "border-line bg-surface"
      }`}
    >
      <Upload className="mb-4 h-10 w-10 text-accent" />
      <p className="text-xl font-semibold text-ink">Open a PDF</p>
      <p className="mt-2 max-w-md text-sm text-muted">
        Drag and drop a file here, or browse. Nam-Dev uploads to the processing API,
        then renders pages with PDF.js.
      </p>
      <label className="mt-6 cursor-pointer rounded-lg bg-accent px-5 py-2.5 text-sm font-medium text-white transition hover:opacity-90">
        {status === "uploading" ? "Uploading…" : "Choose PDF"}
        <input
          type="file"
          accept="application/pdf,.pdf"
          className="hidden"
          disabled={status === "uploading"}
          onChange={(e) => void onFile(e.target.files?.[0])}
        />
      </label>
      {error ? <p className="mt-4 text-sm text-danger">{error}</p> : null}
    </div>
  );
}
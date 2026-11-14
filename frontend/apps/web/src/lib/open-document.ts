"use client";

import { useEffect } from "react";
import { downloadUrl, getDocument } from "@/lib/api";
import { useWorkspaceStore } from "@/store/workspace-store";

/** Load a document from `?doc=` when navigating from the library. */
export function useOpenDocumentFromQuery() {
  const document = useWorkspaceStore((s) => s.document);
  const setDocument = useWorkspaceStore((s) => s.setDocument);
  const setStatus = useWorkspaceStore((s) => s.setStatus);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const docId = params.get("doc");
    if (!docId || document?.id === docId) return;

    let cancelled = false;
    setStatus("uploading");

    void (async () => {
      try {
        const meta = await getDocument(docId);
        const res = await fetch(downloadUrl(docId), { cache: "no-store" });
        if (!res.ok) throw new Error(`Download failed (${res.status})`);
        const blob = await res.blob();
        const localUrl = URL.createObjectURL(blob);
        if (!cancelled) setDocument(meta, localUrl);
      } catch (err) {
        if (!cancelled) {
          setStatus(
            "error",
            err instanceof Error ? err.message : "Failed to open document",
          );
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [document?.id, setDocument, setStatus]);
}
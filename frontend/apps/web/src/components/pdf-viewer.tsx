"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { PDFDocumentProxy } from "pdfjs-dist";
import { loadPdfFromUrl } from "@/lib/pdfjs";
import { PdfPage } from "@/components/pdf-page";
import { useWorkspaceStore } from "@/store/workspace-store";
import type { EditorToolId } from "@/components/annotation-layer";
import type { AnnotationDto, AnnotationUpsert } from "@/lib/annotations-api";

type Props = {
  url: string;
  pageCountHint?: number;
  annotationMode?: boolean;
  annotations?: AnnotationDto[];
  activeTool?: EditorToolId;
  strokeColor?: string;
  onAnnotationsChange?: (next: AnnotationUpsert[]) => void;
};

export function PdfViewer({
  url,
  pageCountHint,
  annotationMode = false,
  annotations = [],
  activeTool = "select",
  strokeColor = "#f2c94c",
  onAnnotationsChange,
}: Props) {
  const [pdf, setPdf] = useState<PDFDocumentProxy | null>(null);
  const [containerWidth, setContainerWidth] = useState(800);
  const [pageWidth, setPageWidth] = useState(612);
  const zoom = useWorkspaceStore((s) => s.zoom);
  const fitMode = useWorkspaceStore((s) => s.fitMode);
  const setCurrentPage = useWorkspaceStore((s) => s.setCurrentPage);

  useEffect(() => {
    let alive = true;
    setPdf(null);
    loadPdfFromUrl(url)
      .then(async (doc) => {
        if (!alive) {
          await doc.destroy();
          return;
        }
        const first = await doc.getPage(1);
        const vp = first.getViewport({ scale: 1 });
        setPageWidth(vp.width);
        setPdf(doc);
      })
      .catch((err) => console.error(err));

    return () => {
      alive = false;
    };
  }, [url]);

  useEffect(() => {
    const onResize = () => {
      const el = document.getElementById("nam-dev-viewer-scroll");
      if (el) setContainerWidth(el.clientWidth - 48);
    };
    onResize();
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, []);

  const scale = useMemo(() => {
    if (fitMode === "width") {
      return Math.max(0.4, Math.min(3, containerWidth / pageWidth));
    }
    if (fitMode === "page") {
      return Math.max(0.4, Math.min(3, (containerWidth * 0.85) / pageWidth));
    }
    return zoom;
  }, [fitMode, zoom, containerWidth, pageWidth]);

  const onVisible = useCallback(
    (pageNumber: number) => setCurrentPage(pageNumber),
    [setCurrentPage],
  );

  if (!pdf) {
    return (
      <div className="flex h-full items-center justify-center text-muted">
        Rendering document…
      </div>
    );
  }

  const pages = pageCountHint && pageCountHint > 0 ? pageCountHint : pdf.numPages;

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-6">
      {Array.from({ length: pages }, (_, i) => i + 1).map((pageNumber) => (
        <PdfPage
          key={pageNumber}
          pdf={pdf}
          pageNumber={pageNumber}
          scale={scale}
          onVisible={onVisible}
          annotationMode={annotationMode}
          annotations={annotations}
          activeTool={activeTool}
          strokeColor={strokeColor}
          onAnnotationsChange={onAnnotationsChange}
        />
      ))}
    </div>
  );
}

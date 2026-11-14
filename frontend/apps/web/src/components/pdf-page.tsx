"use client";

import { useEffect, useRef, useState } from "react";
import type { PDFDocumentProxy } from "pdfjs-dist";
import { AnnotationLayer, type EditorToolId } from "@/components/annotation-layer";
import type { AnnotationDto, AnnotationUpsert } from "@/lib/annotations-api";

type Props = {
  pdf: PDFDocumentProxy;
  pageNumber: number;
  scale: number;
  onVisible?: (pageNumber: number) => void;
  annotations?: AnnotationDto[];
  activeTool?: EditorToolId;
  strokeColor?: string;
  onAnnotationsChange?: (next: AnnotationUpsert[]) => void;
  annotationMode?: boolean;
};

export function PdfPage({
  pdf,
  pageNumber,
  scale,
  onVisible,
  annotations = [],
  activeTool = "select",
  strokeColor = "#f2c94c",
  onAnnotationsChange,
  annotationMode = false,
}: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const [cssSize, setCssSize] = useState({ width: 612, height: 792 });
  const [pagePts, setPagePts] = useState({ width: 612, height: 792 });
  const [rendered, setRendered] = useState(false);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const entry = entries[0];
        if (entry?.isIntersecting) {
          setRendered(true);
          onVisible?.(pageNumber);
        }
      },
      { rootMargin: "400px 0px", threshold: 0.01 },
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, [onVisible, pageNumber]);

  useEffect(() => {
    if (!rendered) return;
    let cancelled = false;
    const canvas = canvasRef.current;
    if (!canvas) return;

    (async () => {
      const page = await pdf.getPage(pageNumber);
      if (cancelled) return;
      const base = page.getViewport({ scale: 1 });
      setPagePts({ width: base.width, height: base.height });
      const viewport = page.getViewport({ scale });
      setCssSize({ width: viewport.width, height: viewport.height });

      const outputScale = window.devicePixelRatio || 1;
      canvas.width = Math.floor(viewport.width * outputScale);
      canvas.height = Math.floor(viewport.height * outputScale);
      canvas.style.width = `${viewport.width}px`;
      canvas.style.height = `${viewport.height}px`;

      const ctx = canvas.getContext("2d");
      if (!ctx) return;
      const transform =
        outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : undefined;

      const task = page.render({
        canvasContext: ctx,
        viewport,
        canvas,
        transform,
      });
      await task.promise;
    })().catch((err) => {
      console.error("PDF page render failed", err);
    });

    return () => {
      cancelled = true;
    };
  }, [pdf, pageNumber, scale, rendered]);

  return (
    <div
      ref={containerRef}
      id={`page-${pageNumber}`}
      className="relative mx-auto mb-6 rounded-sm bg-white shadow-[0_10px_30px_rgba(20,33,43,0.08)] ring-1 ring-line"
      style={{ width: cssSize.width, minHeight: cssSize.height }}
      data-testid={`pdf-page-${pageNumber}`}
    >
      <canvas ref={canvasRef} className="block max-w-full" />
      {annotationMode && onAnnotationsChange ? (
        <AnnotationLayer
          pageIndex={pageNumber - 1}
          pageWidthCss={cssSize.width}
          pageHeightCss={cssSize.height}
          pageWidthPt={pagePts.width}
          pageHeightPt={pagePts.height}
          annotations={annotations}
          activeTool={activeTool}
          strokeColor={strokeColor}
          onChange={onAnnotationsChange}
        />
      ) : null}
    </div>
  );
}

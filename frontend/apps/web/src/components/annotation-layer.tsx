"use client";

import { useRef, useState } from "react";
import type { AnnotationDto, AnnotationUpsert } from "@/lib/annotations-api";

export type EditorToolId =
  | "select"
  | "hand"
  | "text"
  | "highlight"
  | "underline"
  | "pencil"
  | "shapes"
  | "eraser";

type Props = {
  pageIndex: number;
  pageWidthCss: number;
  pageHeightCss: number;
  pageWidthPt: number;
  pageHeightPt: number;
  annotations: AnnotationDto[];
  activeTool: EditorToolId;
  strokeColor: string;
  onChange: (next: AnnotationUpsert[]) => void;
};

type Draft = { x: number; y: number; w: number; h: number };

type InkGeometry = {
  inkPaths?: Array<{ points: Array<{ x: number; y: number }> }>;
};

export function AnnotationLayer({
  pageIndex,
  pageWidthCss,
  pageHeightCss,
  pageWidthPt,
  pageHeightPt,
  annotations,
  activeTool,
  strokeColor,
  onChange,
}: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const start = useRef<{ x: number; y: number } | null>(null);
  const inkPoints = useRef<Array<{ x: number; y: number }>>([]);

  const scaleX = pageWidthPt / Math.max(1, pageWidthCss);
  const scaleY = pageHeightPt / Math.max(1, pageHeightCss);
  const pageAnns = annotations.filter((a) => a.pageIndex === pageIndex);
  const drawing = canDraw(activeTool);
  const isInk = activeTool === "pencil";

  function toLocal(e: React.PointerEvent) {
    const box = ref.current!.getBoundingClientRect();
    return { x: e.clientX - box.left, y: e.clientY - box.top };
  }

  function cssToPdf(d: Draft) {
    const pdfX = d.x * scaleX;
    const pdfW = Math.max(8, d.w) * scaleX;
    const pdfH = Math.max(activeTool === "underline" ? 4 : 12, d.h) * scaleY;
    const pdfY = pageHeightPt - (d.y + Math.max(activeTool === "underline" ? 4 : 12, d.h)) * scaleY;
    return { x: pdfX, y: pdfY, width: pdfW, height: pdfH };
  }

  function localToPdfPoint(p: { x: number; y: number }) {
    return { x: p.x * scaleX, y: pageHeightPt - p.y * scaleY };
  }

  function bboxFromInk(points: Array<{ x: number; y: number }>) {
    const xs = points.map((p) => p.x);
    const ys = points.map((p) => p.y);
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    return {
      x: minX,
      y: minY,
      width: Math.max(8, maxX - minX),
      height: Math.max(8, maxY - minY),
    };
  }

  return (
    <div
      ref={ref}
      className="absolute inset-0"
      style={{ cursor: drawing ? (isInk ? "crosshair" : "crosshair") : "default" }}
      onPointerDown={(e) => {
        if (!drawing) return;
        e.preventDefault();
        const p = toLocal(e);
        start.current = p;
        if (isInk) {
          inkPoints.current = [p];
          setDraft({ x: p.x, y: p.y, w: 0, h: 0 });
        } else {
          setDraft({ x: p.x, y: p.y, w: 0, h: 0 });
        }
        (e.target as HTMLElement).setPointerCapture(e.pointerId);
      }}
      onPointerMove={(e) => {
        if (!start.current) return;
        const p = toLocal(e);
        if (isInk) {
          inkPoints.current.push(p);
          const xs = inkPoints.current.map((pt) => pt.x);
          const ys = inkPoints.current.map((pt) => pt.y);
          const minX = Math.min(...xs);
          const minY = Math.min(...ys);
          setDraft({
            x: minX,
            y: minY,
            w: Math.max(...xs) - minX,
            h: Math.max(...ys) - minY,
          });
        } else {
          setDraft({
            x: Math.min(start.current.x, p.x),
            y: Math.min(start.current.y, p.y),
            w: Math.abs(p.x - start.current.x),
            h: Math.abs(p.y - start.current.y),
          });
        }
      }}
      onPointerUp={() => {
        if (!draft || !drawing) {
          setDraft(null);
          start.current = null;
          inkPoints.current = [];
          return;
        }

        if (activeTool === "eraser") {
          const box = cssToPdf(draft);
          onChange(
            annotations
              .filter((a) => a.pageIndex !== pageIndex || !intersects(a, box))
              .map(toUpsert),
          );
        } else if (isInk && inkPoints.current.length >= 2) {
          const pdfPoints = inkPoints.current.map(localToPdfPoint);
          const box = bboxFromInk(pdfPoints);
          const geometryJson = JSON.stringify({
            inkPaths: [{ points: pdfPoints }],
          } satisfies InkGeometry);
          const created: AnnotationUpsert = {
            id: crypto.randomUUID(),
            type: "Ink",
            pageIndex,
            x: box.x,
            y: box.y,
            width: box.width,
            height: box.height,
            color: strokeColor,
            opacity: 1,
            geometryJson,
          };
          onChange([
            ...annotations.filter((a) => a.pageIndex !== pageIndex).map(toUpsert),
            ...pageAnns.map(toUpsert),
            created,
          ]);
        } else if (draft.w >= 4 || draft.h >= 4 || activeTool === "text") {
          const box = cssToPdf(draft);
          const type = toolToType(activeTool);
          let contents: string | undefined;
          if (activeTool === "text") {
            const entered = window.prompt("Note text", "Note");
            contents = entered === null ? undefined : entered || "Note";
            if (contents === undefined) {
              setDraft(null);
              start.current = null;
              inkPoints.current = [];
              return;
            }
          }
          const created: AnnotationUpsert = {
            id: crypto.randomUUID(),
            type,
            pageIndex,
            x: box.x,
            y: box.y,
            width: box.width,
            height: box.height,
            color: strokeColor,
            opacity: activeTool === "highlight" ? 0.35 : 1,
            contents,
          };
          onChange([
            ...annotations.filter((a) => a.pageIndex !== pageIndex).map(toUpsert),
            ...pageAnns.map(toUpsert),
            created,
          ]);
        }

        setDraft(null);
        start.current = null;
        inkPoints.current = [];
      }}
    >
      {pageAnns.map((a) => {
        const top = pageHeightCss - (a.y + a.height) / scaleY;
        const left = a.x / scaleX;
        const width = a.width / scaleX;
        const height = a.height / scaleY;
        const inkPaths = parseInkPaths(a.geometryJson) ?? [];
        return (
          <div
            key={a.id}
            data-testid={`annotation-${a.id}`}
            className="pointer-events-none absolute overflow-visible"
            style={{ left, top, width, height }}
          >
            {a.type === "Ink" && inkPaths.length > 0 ? (
              <svg
                className="absolute inset-0 h-full w-full overflow-visible"
                viewBox={`0 0 ${pageWidthCss} ${pageHeightCss}`}
                preserveAspectRatio="none"
              >
                {inkPaths.map((path, i) => {
                  const d = path.points
                    .map((pt, idx) => {
                      const lx = pt.x / scaleX - left;
                      const ly = pageHeightCss - pt.y / scaleY - top;
                      return `${idx === 0 ? "M" : "L"} ${lx} ${ly}`;
                    })
                    .join(" ");
                  return (
                    <path
                      key={i}
                      d={d}
                      fill="none"
                      stroke={a.color || "#0f766e"}
                      strokeWidth={2}
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  );
                })}
              </svg>
            ) : (
              <div
                className="absolute inset-0 overflow-hidden"
                style={{
                  background:
                    a.type === "Highlight"
                      ? hexWithAlpha(a.color || "#FFFF00", a.opacity || 0.35)
                      : "transparent",
                  border:
                    a.type === "Rectangle" || a.type === "FreeText"
                      ? `2px solid ${a.color || "#0f766e"}`
                      : undefined,
                  borderBottom:
                    a.type === "Underline" ? `3px solid ${a.color || "#0f766e"}` : undefined,
                  fontSize: 12,
                  color: a.color || "#0f766e",
                }}
              >
                {a.type === "FreeText" ? a.contents || "Note" : null}
              </div>
            )}
          </div>
        );
      })}
      {draft && !isInk ? (
        <div
          className="pointer-events-none absolute border border-dashed border-accent bg-accent/10"
          style={{ left: draft.x, top: draft.y, width: draft.w, height: draft.h }}
        />
      ) : null}
      {draft && isInk && inkPoints.current.length > 1 ? (
        <svg className="pointer-events-none absolute inset-0 h-full w-full">
          <polyline
            points={inkPoints.current.map((p) => `${p.x},${p.y}`).join(" ")}
            fill="none"
            stroke={strokeColor}
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      ) : null}
    </div>
  );
}

function parseInkPaths(geometryJson?: string | null): InkGeometry["inkPaths"] {
  if (!geometryJson) return [];
  try {
    const parsed = JSON.parse(geometryJson) as InkGeometry;
    return parsed.inkPaths ?? [];
  } catch {
    return [];
  }
}

function canDraw(tool: EditorToolId) {
  return (
    tool === "highlight" ||
    tool === "underline" ||
    tool === "shapes" ||
    tool === "text" ||
    tool === "pencil" ||
    tool === "eraser"
  );
}

function toolToType(tool: EditorToolId) {
  switch (tool) {
    case "highlight":
      return "Highlight";
    case "underline":
      return "Underline";
    case "shapes":
      return "Rectangle";
    case "text":
      return "FreeText";
    case "pencil":
      return "Ink";
    default:
      return "Highlight";
  }
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

function intersects(
  a: { x: number; y: number; width: number; height: number },
  b: { x: number; y: number; width: number; height: number },
) {
  return !(
    a.x + a.width < b.x ||
    b.x + b.width < a.x ||
    a.y + a.height < b.y ||
    b.y + b.height < a.y
  );
}

function hexWithAlpha(hex: string, alpha: number) {
  const clean = hex.replace("#", "");
  const full = clean.length === 3 ? clean.split("").map((c) => c + c).join("") : clean;
  const r = parseInt(full.slice(0, 2), 16);
  const g = parseInt(full.slice(2, 4), 16);
  const b = parseInt(full.slice(4, 6), 16);
  return `rgba(${r},${g},${b},${alpha})`;
}
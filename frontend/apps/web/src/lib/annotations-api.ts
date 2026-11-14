"use client";

import { getApiBase } from "@/lib/api";

export type AnnotationDto = {
  id: string;
  documentId: string;
  type: string;
  pageIndex: number;
  x: number;
  y: number;
  width: number;
  height: number;
  color?: string | null;
  opacity: number;
  contents?: string | null;
  author?: string | null;
  geometryJson?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type AnnotationUpsert = {
  id?: string;
  type: string;
  pageIndex: number;
  x: number;
  y: number;
  width: number;
  height: number;
  color?: string;
  opacity?: number;
  contents?: string;
  author?: string;
  geometryJson?: string;
};

export async function listAnnotations(documentId: string): Promise<AnnotationDto[]> {
  const res = await fetch(`${getApiBase()}/api/v1/documents/${documentId}/annotations`, {
    cache: "no-store",
  });
  if (!res.ok) throw new Error(`Failed to load annotations (${res.status})`);
  const json = await res.json();
  return json.items ?? [];
}

export async function saveAnnotations(
  documentId: string,
  annotations: AnnotationUpsert[],
): Promise<AnnotationDto[]> {
  const res = await fetch(`${getApiBase()}/api/v1/documents/${documentId}/annotations`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ annotations }),
  });
  if (!res.ok) throw new Error(`Failed to save annotations (${res.status})`);
  const json = await res.json();
  return json.items ?? [];
}

export async function exportXfdf(documentId: string): Promise<string> {
  const res = await fetch(`${getApiBase()}/api/v1/documents/${documentId}/annotations/xfdf`);
  if (!res.ok) throw new Error(`XFDF export failed (${res.status})`);
  return res.text();
}

export async function importXfdf(documentId: string, xfdfXml: string, replaceExisting = true) {
  const res = await fetch(`${getApiBase()}/api/v1/documents/${documentId}/annotations/xfdf`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ xfdfXml, replaceExisting }),
  });
  if (!res.ok) throw new Error(`XFDF import failed (${res.status})`);
  const json = await res.json();
  return json.items as AnnotationDto[];
}

"use client";

import { authHeaders, useAuthStore } from "@/lib/auth";
import { getApiBase, type DocumentDto, uploadDocument } from "@/lib/api";

export type JobDto = {
  id: string;
  type: string;
  status: string;
  error?: string | null;
  resultFileName?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  hasResult: boolean;
};

function token() {
  return useAuthStore.getState().user?.accessToken;
}

export async function enqueueMerge(documentIds: string[], outputFileName?: string) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/merge`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentIds, outputFileName }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueSplit(documentId: string) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/split`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueRotate(documentId: string, degrees: number) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/rotate`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, degrees }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueCompress(documentId: string, removeMetadata = true) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/compress`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, removeMetadata }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueOrganize(documentId: string, pageOrder: number[]) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/organize`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, pageOrder }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueWatermark(documentId: string, text: string, opacity = 0.25) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/watermark`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, text, opacity }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueProtect(documentId: string, userPassword: string) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/protect`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, userPassword }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function imagesToPdf(files: File[]): Promise<Blob> {
  const form = new FormData();
  for (const file of files) form.append("files", file);
  const res = await fetch(`${getApiBase()}/api/v1/pdf/images-to-pdf`, {
    method: "POST",
    headers: { ...authHeaders(token()) },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res.blob();
}

export async function enqueueExtractPages(documentId: string, pageIndexes: number[]) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/extract-pages`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, pageIndexes }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueReverse(documentId: string) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/reverse`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueDeletePages(documentId: string, pageIndexes: number[]) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/delete-pages`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, pageIndexes }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueuePageNumbers(documentId: string, format = "{n} / {total}") {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/page-numbers`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, format }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueueUnlock(documentId: string, userPassword: string) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/unlock`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, userPassword }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function enqueuePdfToImages(documentId: string, dpi = 144) {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/to-images`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders(token()) },
    body: JSON.stringify({ documentId, dpi }),
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export async function getJob(id: string) {
  const res = await fetch(`${getApiBase()}/api/v1/jobs/${id}`, {
    headers: { ...authHeaders(token()) },
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as JobDto;
}

export function jobDownloadUrl(id: string) {
  return `${getApiBase()}/api/v1/jobs/${id}/download`;
}

export async function uploadMany(files: File[]): Promise<DocumentDto[]> {
  const docs: DocumentDto[] = [];
  const accessToken = token();
  for (const file of files) {
    docs.push(await uploadDocument(file, accessToken));
  }
  return docs;
}

export async function pollJob(id: string, timeoutMs = 60000): Promise<JobDto> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const job = await getJob(id);
    if (job.status === "Succeeded" || job.status === "Failed") return job;
    await new Promise((r) => setTimeout(r, 800));
  }
  throw new Error("Job timed out");
}

async function corePdf(path: string, form: FormData): Promise<Blob> {
  const res = await fetch(`${getApiBase()}/api/v1/pdf/core/${path}`, {
    method: "POST",
    headers: { ...authHeaders(token()) },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res.blob();
}

export async function coreRepair(file: File): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  return corePdf("repair", form);
}

export async function coreToMarkdown(file: File): Promise<string> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`${getApiBase()}/api/v1/pdf/core/to-markdown`, {
    method: "POST",
    headers: { ...authHeaders(token()) },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res.text();
}

export async function coreCompare(left: File, right: File): Promise<unknown> {
  const form = new FormData();
  form.append("left", left);
  form.append("right", right);
  const res = await fetch(`${getApiBase()}/api/v1/pdf/core/compare`, {
    method: "POST",
    headers: { ...authHeaders(token()) },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function coreHtmlToPdf(html: string, title?: string): Promise<Blob> {
  const form = new FormData();
  form.append("html", html);
  if (title) form.append("title", title);
  return corePdf("html-to-pdf", form);
}

export async function coreSignStamp(
  file: File,
  options: { pageIndex: number; xPoints: number; yPoints: number; signerName: string },
): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  form.append("options", JSON.stringify(options));
  return corePdf("sign-stamp", form);
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

export { downloadBlob };

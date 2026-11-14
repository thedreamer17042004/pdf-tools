import { authHeaders } from "@/lib/auth";

export type DocumentDto = {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  pageCount: number;
  createdAtUtc: string;
};

export type DocumentTextDto = {
  documentId: string;
  pages: { pageIndex: number; text: string }[];
  fullText: string;
};

const API_BASE =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ?? "http://localhost:5080";

export function getApiBase() {
  return API_BASE;
}

export async function uploadDocument(
  file: File,
  token?: string | null,
): Promise<DocumentDto> {
  const form = new FormData();
  form.append("file", file);

  const res = await fetch(`${API_BASE}/api/v1/documents`, {
    method: "POST",
    headers: authHeaders(token),
    body: form,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(parseApiError(text) || `Upload failed (${res.status})`);
  }

  return res.json();
}

export async function listMyDocuments(token: string): Promise<DocumentDto[]> {
  const res = await fetch(`${API_BASE}/api/v1/documents/mine`, {
    headers: authHeaders(token),
    cache: "no-store",
  });
  if (!res.ok) {
    throw new Error(parseApiError(await res.text()) || `List failed (${res.status})`);
  }
  const json = (await res.json()) as { items: DocumentDto[] };
  return json.items ?? [];
}

export async function deleteDocument(id: string, token: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/v1/documents/${id}`, {
    method: "DELETE",
    headers: authHeaders(token),
  });
  if (!res.ok && res.status !== 204) {
    throw new Error(parseApiError(await res.text()) || `Delete failed (${res.status})`);
  }
}

export async function extractText(id: string): Promise<DocumentTextDto> {
  const res = await fetch(`${API_BASE}/api/v1/documents/${id}/text`, {
    cache: "no-store",
  });
  if (!res.ok) {
    throw new Error(parseApiError(await res.text()) || `Extract failed (${res.status})`);
  }
  return res.json();
}

export async function getDocument(id: string): Promise<DocumentDto> {
  const res = await fetch(`${API_BASE}/api/v1/documents/${id}`, {
    cache: "no-store",
  });
  if (!res.ok) {
    throw new Error(`Failed to load document (${res.status})`);
  }
  return res.json();
}

export function downloadUrl(id: string) {
  return `${API_BASE}/api/v1/documents/${id}/download`;
}

export function serverRenderUrl(id: string, page: number, dpi = 144) {
  return `${API_BASE}/api/v1/documents/${id}/render?page=${page}&dpi=${dpi}`;
}

export async function loadDocumentBlob(id: string): Promise<Blob> {
  const res = await fetch(downloadUrl(id), { cache: "no-store" });
  if (!res.ok) {
    throw new Error(`Download failed (${res.status})`);
  }
  return res.blob();
}

function parseApiError(text: string): string | null {
  try {
    const json = JSON.parse(text) as { error?: { message?: string } };
    return json.error?.message ?? null;
  } catch {
    return null;
  }
}
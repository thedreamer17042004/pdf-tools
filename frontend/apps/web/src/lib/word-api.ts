"use client";

import { authHeaders, useAuthStore } from "@/lib/auth";
import { getApiBase } from "@/lib/api";

function token() {
  return useAuthStore.getState().user?.accessToken;
}

export type WordInfo = {
  title?: string | null;
  author?: string | null;
  subject?: string | null;
  creator?: string | null;
  paragraphCount: number;
  tableCount: number;
  mergeFields: string[];
};

async function postForm(path: string, form: FormData): Promise<Response> {
  const res = await fetch(`${getApiBase()}/api/v1/word/core/${path}`, {
    method: "POST",
    headers: { ...authHeaders(token()) },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return res;
}

export async function wordCreate(text: string, title?: string): Promise<Blob> {
  const form = new FormData();
  form.append("text", text);
  if (title) form.append("title", title);
  return (await postForm("create", form)).blob();
}

export async function wordInfo(file: File): Promise<WordInfo> {
  const form = new FormData();
  form.append("file", file);
  return (await postForm("info", form)).json() as Promise<WordInfo>;
}

export async function wordExtractText(file: File): Promise<string> {
  const form = new FormData();
  form.append("file", file);
  const data = (await (await postForm("extract-text", form)).json()) as { text: string };
  return data.text;
}

export async function wordListMergeFields(file: File): Promise<string[]> {
  const form = new FormData();
  form.append("file", file);
  const data = (await (await postForm("merge-fields/list", form)).json()) as {
    items: string[];
  };
  return data.items;
}

export async function wordMergeFields(
  file: File,
  values: Record<string, string>,
): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  form.append("options", JSON.stringify({ values }));
  return (await postForm("merge-fields", form)).blob();
}

export async function wordReplaceText(
  file: File,
  find: string,
  replace: string,
): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  form.append("options", JSON.stringify({ find, replace }));
  return (await postForm("replace-text", form)).blob();
}

export async function wordAppend(file: File, appendix: File): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  form.append("appendix", appendix);
  return (await postForm("append", form)).blob();
}

export async function wordToPdf(file: File): Promise<Blob> {
  const form = new FormData();
  form.append("file", file);
  return (await postForm("to-pdf", form)).blob();
}

export function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  a.click();
  URL.revokeObjectURL(url);
}

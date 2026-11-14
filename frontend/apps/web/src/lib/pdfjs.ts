"use client";

import { GlobalWorkerOptions, getDocument, version, type PDFDocumentProxy } from "pdfjs-dist";

let workerReady = false;

export function ensurePdfWorker() {
  if (workerReady) return;
  GlobalWorkerOptions.workerSrc = `https://unpkg.com/pdfjs-dist@${version}/build/pdf.worker.min.mjs`;
  workerReady = true;
}

export async function loadPdfFromUrl(url: string): Promise<PDFDocumentProxy> {
  ensurePdfWorker();
  const loadingTask = getDocument({ url, withCredentials: false });
  return loadingTask.promise;
}

export async function loadPdfFromData(data: ArrayBuffer): Promise<PDFDocumentProxy> {
  ensurePdfWorker();
  const loadingTask = getDocument({ data });
  return loadingTask.promise;
}

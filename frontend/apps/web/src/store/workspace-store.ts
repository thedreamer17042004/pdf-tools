import { create } from "zustand";
import type { DocumentDto } from "@/lib/api";

export type FitMode = "width" | "page" | "custom";

type WorkspaceState = {
  document: DocumentDto | null;
  localUrl: string | null;
  currentPage: number;
  zoom: number;
  fitMode: FitMode;
  status: "idle" | "uploading" | "ready" | "error";
  error: string | null;
  showServerRender: boolean;
  setDocument: (doc: DocumentDto, localUrl: string) => void;
  setCurrentPage: (page: number) => void;
  setZoom: (zoom: number) => void;
  setFitMode: (mode: FitMode) => void;
  setStatus: (status: WorkspaceState["status"], error?: string | null) => void;
  toggleServerRender: () => void;
  reset: () => void;
};

export const useWorkspaceStore = create<WorkspaceState>((set) => ({
  document: null,
  localUrl: null,
  currentPage: 1,
  zoom: 1,
  fitMode: "width",
  status: "idle",
  error: null,
  showServerRender: false,
  setDocument: (document, localUrl) =>
    set({
      document,
      localUrl,
      currentPage: 1,
      status: "ready",
      error: null,
    }),
  setCurrentPage: (currentPage) => set({ currentPage }),
  setZoom: (zoom) => set({ zoom, fitMode: "custom" }),
  setFitMode: (fitMode) => set({ fitMode }),
  setStatus: (status, error = null) => set({ status, error }),
  toggleServerRender: () =>
    set((s) => ({ showServerRender: !s.showServerRender })),
  reset: () =>
    set({
      document: null,
      localUrl: null,
      currentPage: 1,
      zoom: 1,
      fitMode: "width",
      status: "idle",
      error: null,
      showServerRender: false,
    }),
}));

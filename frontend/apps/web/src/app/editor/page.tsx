"use client";

import dynamic from "next/dynamic";

const EditorShell = dynamic(
  () => import("@/components/editor-shell").then((m) => m.EditorShell),
  {
    ssr: false,
    loading: () => (
      <div className="flex min-h-screen items-center justify-center text-muted">
        Loading editor…
      </div>
    ),
  },
);

export default function EditorPage() {
  return <EditorShell />;
}

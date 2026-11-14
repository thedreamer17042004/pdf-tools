"use client";

import dynamic from "next/dynamic";

const WorkspaceShell = dynamic(
  () =>
    import("@/components/workspace-shell").then((m) => m.WorkspaceShell),
  {
    ssr: false,
    loading: () => (
      <div className="flex min-h-screen items-center justify-center text-muted">
        Loading Nam-Dev workspace…
      </div>
    ),
  },
);

export default function WorkspacePage() {
  return <WorkspaceShell />;
}

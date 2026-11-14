import { expect, test } from "@playwright/test";
import path from "node:path";

const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5080";
const samplePdf = path.resolve(__dirname, "../../../../samples/hello.pdf");

test.describe("Nam-Dev final smoke", () => {
  test("home, login, library, tools, editor routes render", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("Nam-Dev").first()).toBeVisible();
    await expect(page.getByRole("link", { name: /Open library/i })).toBeVisible();

    await page.goto("/login");
    await expect(page.getByRole("heading", { name: /Sign in|Create account/i })).toBeVisible();
    await expect(page.getByPlaceholder(/Email/i)).toBeVisible();

    await page.goto("/library");
    await expect(page.getByText(/Sign in|Library|document/i).first()).toBeVisible();

    await page.goto("/tools");
    await expect(page.getByText(/Sign in|PDF Tools/i).first()).toBeVisible();

    await page.goto("/editor");
    await expect(page.getByText("Editor").first()).toBeVisible();
    await expect(page.getByTestId("tool-highlight")).toBeVisible();
  });

  test("annotation API save + XFDF export", async ({ request }) => {
    const health = await request.get(`${apiURL}/health`);
    test.skip(!health.ok(), "API not running");

    const fs = await import("node:fs");
    test.skip(!fs.existsSync(samplePdf), "sample PDF missing");

    const form = new FormData();
    const bytes = fs.readFileSync(samplePdf);
    form.append("file", new Blob([bytes], { type: "application/pdf" }), "hello.pdf");

    const upload = await request.post(`${apiURL}/api/v1/documents`, { multipart: { file: {
      name: "hello.pdf",
      mimeType: "application/pdf",
      buffer: bytes,
    } } });
    expect(upload.ok()).toBeTruthy();
    const doc = await upload.json();
    const documentId = doc.id as string;

    const put = await request.put(`${apiURL}/api/v1/documents/${documentId}/annotations`, {
      data: {
        annotations: [
          {
            type: "Highlight",
            pageIndex: 0,
            x: 72,
            y: 700,
            width: 120,
            height: 18,
            color: "#FFFF00",
            opacity: 0.35,
            contents: "e2e",
          },
        ],
      },
    });
    expect(put.ok()).toBeTruthy();
    const saved = await put.json();
    expect(saved.items.length).toBe(1);

    const xfdf = await request.get(`${apiURL}/api/v1/documents/${documentId}/annotations/xfdf`);
    expect(xfdf.ok()).toBeTruthy();
    const xml = await xfdf.text();
    expect(xml).toContain("highlight");
  });

  test("register + compress tool job", async ({ request }) => {
    const health = await request.get(`${apiURL}/health`);
    test.skip(!health.ok(), "API not running");

    const fs = await import("node:fs");
    test.skip(!fs.existsSync(samplePdf), "sample PDF missing");

    const email = `phase3_${Date.now()}@example.com`;
    const password = "Phase3!Test";

    const register = await request.post(`${apiURL}/api/v1/auth/register`, {
      data: { email, password, displayName: "Phase3" },
    });
    // 200/201 or 400 if already exists — try login either way
    const login = await request.post(`${apiURL}/api/v1/auth/login`, {
      data: { email, password },
    });
    if (!login.ok()) {
      test.skip(true, `auth unavailable (${register.status()}/${login.status()})`);
    }
    const auth = await login.json();
    const token = auth.accessToken as string;

    const bytes = fs.readFileSync(samplePdf);
    const upload = await request.post(`${apiURL}/api/v1/documents`, {
      multipart: {
        file: { name: "hello.pdf", mimeType: "application/pdf", buffer: bytes },
      },
    });
    expect(upload.ok()).toBeTruthy();
    const doc = await upload.json();

    const compress = await request.post(`${apiURL}/api/v1/pdf/compress`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { documentId: doc.id, removeMetadata: true },
    });
    expect(compress.ok() || compress.status() === 202).toBeTruthy();
    const job = await compress.json();

    let status = job.status as string;
    for (let i = 0; i < 40 && status !== "Succeeded" && status !== "Failed"; i++) {
      await new Promise((r) => setTimeout(r, 500));
      const poll = await request.get(`${apiURL}/api/v1/jobs/${job.id}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const body = await poll.json();
      status = body.status;
      if (status === "Succeeded") {
        expect(body.hasResult).toBeTruthy();
        return;
      }
    }
    expect(status).toBe("Succeeded");
  });
});

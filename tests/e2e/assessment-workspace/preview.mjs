// Read-only UI preview: no operation is forwarded to the shared synthetic host.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { resolve, extname } from "node:path";

const source = "http://127.0.0.1:5183";
const data = new Map();
const read = async (path) => {
  for (let attempt = 0; attempt < 40; attempt++) {
    try {
      const response = await fetch(`${source}${path}`);
      if (response.ok) return response.json();
    } catch {
      /* Another independent pilot test may be restarting its owned host. */
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(
    "Read-only synthetic snapshot unavailable. Run the synthetic host first.",
  );
};
const catalog = await read("/local-demo/v1/catalog");
const history = await read("/local-demo/v1/runs");
const ready = history.runs.filter((run) => run.state === "Scoring");
history.runs = [
  ready.find((run) =>
    run.selection.profileId.startsWith("synthetic-review-maturity-"),
  ),
  ready.find((run) => run.selection.profileId === "profile-standard"),
].filter(Boolean);
data.set("/local-demo/v1/catalog", catalog);
data.set("/local-demo/v1/runs", history);
for (const run of history.runs) {
  const path = `/local-demo/v1/runs/${run.runId}`;
  data.set(path, await read(path));
  if (run.selection.profileId.startsWith("synthetic-")) {
    data.set(`${path}/analysis`, await read(`${path}/analysis`));
    if (run.selection.profileId.startsWith("synthetic-review-")) {
      data.set(`${path}/review`, await read(`${path}/review`));
    }
  }
}
// These local fixture tokens are intentionally replaced, never stored or forwarded.
catalog.csrfToken = "readonly-workspace-preview";
const root = resolve("src/web/dist");
const server = createServer(async (request, response) => {
  const path = new URL(request.url, "http://127.0.0.1:5197").pathname;
  response.setHeader(
    "Content-Security-Policy",
    "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'",
  );
  if (request.method !== "GET") {
    response.writeHead(503, { "Content-Type": "application/json" });
    response.end(
      JSON.stringify({
        code: "Unavailable",
        message:
          "Read-only UI preview. Start, review, cancel and resume are unavailable here.",
      }),
    );
  } else if (data.has(path)) {
    response.writeHead(200, {
      "Content-Type": "application/json",
      "Cache-Control": "no-store",
    });
    response.end(JSON.stringify(data.get(path)));
  } else if (path.startsWith("/local-demo/")) {
    response.writeHead(404);
    response.end();
  } else {
    const file = resolve(root, `.${path === "/" ? "/index.html" : path}`);
    if (!file.startsWith(`${root}/`)) {
      response.writeHead(403);
      response.end();
      return;
    }
    try {
      const body = await readFile(file);
      response.writeHead(200, {
        "Content-Type":
          {
            ".html": "text/html",
            ".js": "text/javascript",
            ".css": "text/css",
          }[extname(file)] ?? "application/octet-stream",
      });
      response.end(body);
    } catch {
      response.writeHead(404);
      response.end();
    }
  }
});
server.listen(5197, "127.0.0.1", () =>
  console.log("Read-only workspace preview: http://127.0.0.1:5197"),
);
process.on("SIGINT", () => server.close(() => process.exit(0)));
process.on("SIGTERM", () => server.close(() => process.exit(0)));

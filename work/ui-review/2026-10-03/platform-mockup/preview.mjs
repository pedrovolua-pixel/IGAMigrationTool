import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { resolve, extname, sep } from "node:path";
const root = fileURLToPath(new URL(".", import.meta.url));
const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".png": "image/png",
};
const server = createServer(async (request, response) => {
  response.setHeader("Cache-Control", "no-store");
  response.setHeader("X-Content-Type-Options", "nosniff");
  response.setHeader(
    "Content-Security-Policy",
    "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'none'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'",
  );
  if (request.method !== "GET") {
    response.writeHead(405, { Allow: "GET" });
    response.end("Mockup only.");
    return;
  }
  try {
    const pathname = decodeURIComponent(
      new URL(request.url, "http://127.0.0.1:5211").pathname,
    );
    const path = resolve(
      root,
      "." + (pathname === "/" ? "/index.html" : pathname),
    );
    if (
      !path.startsWith(root.endsWith(sep) ? root : root + sep) ||
      !types[extname(path)]
    ) {
      response.writeHead(404);
      response.end();
      return;
    }
    const data = await readFile(path);
    response.writeHead(200, { "Content-Type": types[extname(path)] });
    response.end(data);
  } catch {
    response.writeHead(404);
    response.end();
  }
});
server.listen(5211, "127.0.0.1", () =>
  console.log("Complete platform mockup: http://127.0.0.1:5211/"),
);
for (const signal of ["SIGINT", "SIGTERM"])
  process.on(signal, () => server.close(() => process.exit(0)));

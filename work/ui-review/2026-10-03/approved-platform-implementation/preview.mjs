// Frozen synthetic snapshot; this server never contacts or forwards to a backend.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
const folder = fileURLToPath(new URL('.', import.meta.url));
const root = resolve(folder, 'preview-assets');
const data = new Map(JSON.parse(await readFile(resolve(folder, 'preview-snapshot.json'), 'utf8')));
const server = createServer(async (request, response) => {
  response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'");
  response.setHeader('X-Content-Type-Options', 'nosniff');
  response.setHeader('Cache-Control', 'no-store');
  const path = new URL(request.url, 'http://127.0.0.1:5212').pathname;
  if (request.method !== 'GET') {
    response.writeHead(503, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify({ code: 'Unavailable', message: 'Read-only UI preview. Start, review, cancel and resume are unavailable here.' }));
  } else if (data.has(path)) {
    response.writeHead(200, { 'Content-Type': 'application/json' });
    response.end(JSON.stringify(data.get(path)));
  } else if (path.startsWith('/local-demo/')) {
    response.writeHead(404); response.end();
  } else {
    if (path.startsWith('/design-review')) response.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'");
    const file = resolve(root, `.${path === '/' ? '/index.html' : path === '/design-review/' ? '/design-review/index.html' : path}`);
    if (!file.startsWith(`${root}/`)) { response.writeHead(403); response.end(); return; }
    try {
      const body = await readFile(file);
      response.writeHead(200, { 'Content-Type': { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml' }[extname(file)] ?? 'application/octet-stream' });
      response.end(body);
    } catch { response.writeHead(404); response.end(); }
  }
});
server.listen(5212, '127.0.0.1', () => console.log('Readonly preview: http://127.0.0.1:5212'));
process.on('SIGINT', () => server.close(() => process.exit(0)));
process.on('SIGTERM', () => server.close(() => process.exit(0)));

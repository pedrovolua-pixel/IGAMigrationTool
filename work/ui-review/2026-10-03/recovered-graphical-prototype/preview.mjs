import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
const html = await readFile(new URL('./index.html', import.meta.url));
const server = createServer((request, response) => {
  response.setHeader('Cache-Control', 'no-store');
  response.setHeader('X-Content-Type-Options', 'nosniff');
  if (request.method !== 'GET') {
    response.writeHead(405, { Allow: 'GET' });
    response.end('Design preview only. No backend operations.');
  } else if (new URL(request.url, 'http://127.0.0.1:5210').pathname === '/') {
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    response.end(html);
  } else {
    response.writeHead(404);
    response.end();
  }
});
server.listen(5210, '127.0.0.1', () => console.log('Recovered graphical prototype: http://127.0.0.1:5210/'));
process.on('SIGINT', () => server.close(() => process.exit(0)));
process.on('SIGTERM', () => server.close(() => process.exit(0)));

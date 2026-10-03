export default {
  root: new URL('.', import.meta.url).pathname,
  base: '/',
  build: { outDir: 'dist', emptyOutDir: true },
};

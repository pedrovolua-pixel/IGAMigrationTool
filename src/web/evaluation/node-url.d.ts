// Build-only declaration for the pinned Node builtin; no browser runtime or package dependency.
declare module 'node:url' {
  export function fileURLToPath(url: URL): string;
}

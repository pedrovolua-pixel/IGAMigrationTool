import type { DemoError } from './demo-contract.generated';

const prefix = '/local-demo/v1';

export class DemoRequestError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function request<T>(
  path: string,
  signal?: AbortSignal,
  body?: object,
  csrfToken?: string,
): Promise<T> {
  const response = await fetch(`${prefix}${path}`, {
    method: body ? 'POST' : 'GET',
    credentials: 'same-origin',
    cache: 'no-store',
    signal,
    headers: body
      ? { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken ?? '' }
      : { Accept: 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const json: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const error = json as Partial<DemoError> | null;
    const message =
      typeof error?.message === 'string' && error.message.length <= 1024
        ? error.message
        : 'The local host could not complete this request. Refresh the run and try again.';
    throw new DemoRequestError(response.status, message);
  }
  if (
    !json ||
    typeof json !== 'object' ||
    !('schemaVersion' in json) ||
    json.schemaVersion !== 1 ||
    !('demoOnly' in json) ||
    json.demoOnly !== true
  ) {
    throw new Error(
      'The host returned an unsupported demo response. Run data has not been changed.',
    );
  }
  return json as T;
}

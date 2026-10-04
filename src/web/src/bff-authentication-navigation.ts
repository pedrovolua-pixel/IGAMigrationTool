import type { BffSession } from './bff-authentication-contract.generated';

/** Unused local transport helper. Production composition remains separately gated. */
export async function getBffSession(): Promise<BffSession> {
  const response = await fetch('/bff/v1/session', {
    credentials: 'same-origin',
    cache: 'no-store',
    redirect: 'error',
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) throw new Error('Session transport denied.');
  const value: unknown = await response.json();
  if (
    typeof value !== 'object' ||
    value === null ||
    Object.keys(value).sort().join(',') !== 'authenticated,csrfToken,schemaVersion' ||
    !('schemaVersion' in value) ||
    value.schemaVersion !== 'bff-session-v1' ||
    !('authenticated' in value) ||
    typeof value.authenticated !== 'boolean' ||
    !('csrfToken' in value) ||
    typeof value.csrfToken !== 'string' ||
    value.csrfToken.length === 0
  ) {
    throw new Error('Invalid session transport response.');
  }
  return value as BffSession;
}

/** Native navigation preserves the supported provider redirect and callback cookies. */
export async function navigateBffSignIn(): Promise<void> {
  const session = await getBffSession();
  if (session.authenticated) throw new Error('Already authenticated.');
  const form = document.createElement('form');
  form.method = 'post';
  form.action = '/bff/v1/sign-in';
  form.enctype = 'application/x-www-form-urlencoded';
  form.acceptCharset = 'UTF-8';
  form.target = '_self';
  form.hidden = true;
  const token = document.createElement('input');
  token.type = 'hidden';
  token.name = '__RequestVerificationToken';
  token.value = session.csrfToken;
  form.append(token);
  document.body.append(form);
  try {
    form.submit();
  } finally {
    form.remove();
  }
}

export async function signOutBffSession(): Promise<void> {
  const session = await getBffSession();
  if (!session.authenticated) throw new Error('Authentication required.');
  const response = await fetch('/bff/v1/sign-out', {
    method: 'POST',
    credentials: 'same-origin',
    cache: 'no-store',
    redirect: 'error',
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'X-CSRF-Token': session.csrfToken,
    },
    body: '{}',
  });
  if (response.status !== 204) throw new Error('Local sign-out denied.');
}

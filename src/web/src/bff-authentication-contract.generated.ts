// Generated from contracts/bff-authentication/bff-v1.openapi.json. Do not edit.
// Approved local D01/D02 contract; no production host activation or demo UI adoption.

export type BffSession = {
  readonly schemaVersion: "bff-session-v1";
  readonly authenticated: boolean;
  readonly csrfToken: string;
};

export type BffSignInForm = {
  readonly __RequestVerificationToken: string;
};

export type BffSignOutBody = Readonly<Record<string, never>>;

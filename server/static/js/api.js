// Signing in and calling the API. The access token lives in this module's memory only: not in localStorage, not in a cookie, not in the address. Reloading the page signs the person out; that is deliberate.

let config = null;
let tokens = null;        // { access, refresh, expiresAt } — expiresAt counts on the page's own timer (performance.now), not on the computer's date
let refreshTimer = null;
let onSignedOut = () => {};

export const ApiError = class extends Error {
  constructor(status, detail) { super(typeof detail === 'string' ? detail : (detail && detail.code) || `error ${status}`); this.status = status; this.detail = detail; }
};

export async function loadConfig() {
  if (!config) config = await (await fetch('/config.json', { cache: 'no-cache' })).json();
  return config;
}

export function setSignedOutHandler(fn) { onSignedOut = fn; }
export function isSignedIn() { return tokens !== null; }

function authUrl(path) { return `${config.supabase_url}/auth/v1${path}`; }
function authHeaders(extra = {}) { return { 'Content-Type': 'application/json', apikey: config.supabase_publishable_key, ...extra }; }

function keep(body) {
  tokens = { access: body.access_token, refresh: body.refresh_token, expiresAt: performance.now() + Math.max(30, Number(body.expires_in) || 3600) * 1000 };
  clearTimeout(refreshTimer);
  const wait = Math.max(10_000, tokens.expiresAt - performance.now() - 60_000);   // renew a minute before it runs out
  refreshTimer = setTimeout(() => { refresh().catch(() => {}); }, wait);
}

export async function signIn(email, password) {
  await loadConfig();
  const response = await fetch(authUrl('/token?grant_type=password'), { method: 'POST', headers: authHeaders(), body: JSON.stringify({ email, password }) });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new ApiError(response.status, 'Email or password is not right.');
  keep(body);
}

export async function refresh() {
  if (!tokens || !tokens.refresh) throw new ApiError(401, 'signed_out');
  const response = await fetch(authUrl('/token?grant_type=refresh_token'), { method: 'POST', headers: authHeaders(), body: JSON.stringify({ refresh_token: tokens.refresh }) });
  if (!response.ok) { signOut(); throw new ApiError(401, 'signed_out'); }
  keep(await response.json());
}

export function signOut() {
  tokens = null;
  clearTimeout(refreshTimer);
  onSignedOut();
}

export async function requestPasswordReset(email) {
  await loadConfig();
  await fetch(authUrl('/recover'), { method: 'POST', headers: authHeaders(), body: JSON.stringify({ email }) });   // the answer is the same whether the address exists or not
}

export async function setNewPassword(recoveryToken, password) {
  await loadConfig();
  const response = await fetch(authUrl('/user'), { method: 'PUT', headers: authHeaders({ Authorization: `Bearer ${recoveryToken}` }), body: JSON.stringify({ password }) });
  if (!response.ok) throw new ApiError(response.status, 'The password could not be changed. The link may have expired; ask for a new one.');
}

async function call(method, path, { query, body, raw } = {}, retry = true) {
  if (!tokens) throw new ApiError(401, 'signed_out');
  const url = new URL(path, location.origin);
  for (const [k, v] of Object.entries(query || {})) if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, v);
  const response = await fetch(url, { method, headers: { Authorization: `Bearer ${tokens.access}`, ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) }, body: body !== undefined ? JSON.stringify(body) : undefined, cache: 'no-store' });
  if (response.status === 401 && retry) { await refresh(); return call(method, path, { query, body, raw }, false); }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    if (response.status === 403 && problem.detail === 'membership_deactivated') { signOut(); }
    throw new ApiError(response.status, problem.detail);
  }
  if (response.status === 204) return null;
  return raw ? response.text() : response.json();
}

export const api = {
  get: (path, query) => call('GET', path, { query }),
  getText: (path) => call('GET', path, { raw: true }),
  post: (path, body) => call('POST', path, { body: body ?? {} }),
  put: (path, body) => call('PUT', path, { body: body ?? {} }),
  patch: (path, body) => call('PATCH', path, { body }),
  del: (path) => call('DELETE', path),
};

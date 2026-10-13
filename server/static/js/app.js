// Start-up, navigation and the messages line. The page is one document: moving between views keeps the access token in memory, which is why the address uses #/ routes.
import { ApiError, api, isSignedIn, loadConfig, setSignedOutHandler, signOut } from './api.js';
import { clear, h } from './dom.js';
import { consentView, deviceView, noOrganisationView, overviewView, peopleView, privacyView, progressView, reportView, resetView, sessionsView, signInView } from './views.js';

const main = document.getElementById('main');
const nav = document.getElementById('nav');
const who = document.getElementById('who');
const statusLine = document.getElementById('status');
let me = null;
let consent = null;       // the answer to GET /privacy/consent: whether the person must answer before anything else
let org = null;           // the membership currently shown: { organisation_id, organisation_name, role, ... }
let shown = 0;            // numbers each rendering so a slow answer cannot overwrite a newer page

const RANK = { trainee: 0, supervisor: 1, org_admin: 2 };

const ctx = {
  get org() { return org; },
  get me() { return me; },
  say(text, kind = '') {
    clear(statusLine).appendChild(h('div', { class: `msg ${kind}`, text }));
  },
  go(route) {
    if (route.startsWith('/')) { history.pushState(null, '', route); render(); } else { location.hash = route; }
  },
  show(make) { return display(make); },
  async refreshConsent() { consent = await api.get('/privacy/consent'); },
};

function setNav() {
  clear(nav); clear(who);
  if (!me || !org) return;
  const here = location.hash || '#/progress';
  const links = [['#/progress', 'My progress'], ['#/sessions', 'Sessions']];
  if (RANK[org.role] >= RANK.supervisor) links.push(['#/overview', 'Overview']);
  if (RANK[org.role] >= RANK.org_admin) links.push(['#/people', 'People']);
  links.push(['/device', 'Sign in a headset'], ['#/privacy', 'Privacy']);
  for (const [href, text] of links) {
    const inPage = href.startsWith('/');   // /device is a path: go there without reloading, so the token stays in memory
    nav.appendChild(h('a', { href, text, 'aria-current': href === here.split('/').slice(0, 2).join('/') && location.pathname === '/' ? 'page' : (inPage && href === location.pathname ? 'page' : null),
      onclick: inPage ? (e) => { e.preventDefault(); ctx.go(href); } : null }));
  }
  const memberships = me.memberships.filter((m) => m.status === 'active');
  if (memberships.length > 1) {
    who.appendChild(h('select', { 'aria-label': 'Organisation', onchange: (e) => { org = memberships.find((m) => m.organisation_id === e.target.value); location.hash = '#/progress'; render(); } },
      memberships.map((m) => h('option', { value: m.organisation_id, selected: m.organisation_id === org.organisation_id, text: m.organisation_name }))));
  } else {
    who.appendChild(h('span', { text: org.organisation_name }));
  }
  who.appendChild(h('span', { text: `${me.pseudonym} · ${org.role.replace('_', ' ')}` }));
  who.appendChild(h('button', { class: 'secondary', text: 'Sign out', onclick: () => signOut() }));
}

async function display(make) {
  const mine = ++shown;
  clear(statusLine);
  try {
    const view = await make();
    if (mine !== shown) return;
    clear(main).appendChild(view);
    main.focus({ preventScroll: true });
    document.title = (main.querySelector('h1')?.textContent || 'ProcSense') + ' · ProcSense';
  } catch (e) {
    if (mine !== shown) return;
    if (e instanceof ApiError && e.status === 401) return;       // signed out: the sign-in page takes over
    clear(main).appendChild(h('section', {}, h('h1', { text: 'This page could not be shown' }), h('p', { class: 'notice', text: e instanceof ApiError && e.status === 404 ? 'It does not exist, or you may not see it.' : e instanceof ApiError && e.status === 403 ? 'You are not allowed to see this.' : 'Something went wrong. Try again in a moment.' })));
  }
}

async function route() {
  const parts = (location.hash.replace(/^#\/?/, '') || 'progress').split('/');
  const need = (rank) => RANK[org.role] >= rank;
  switch (parts[0]) {
    case 'privacy': return display(() => privacyView(ctx, consent));
    case 'sessions': return display(() => sessionsView(ctx));
    case 'report': return display(() => reportView(ctx, parts[1]));
    case 'overview': return need(RANK.supervisor) ? display(() => overviewView(ctx)) : display(() => progressView(ctx));
    case 'person': return need(RANK.supervisor) ? display(() => progressView(ctx, parts[1])) : display(() => progressView(ctx));
    case 'people': return need(RANK.org_admin) ? display(() => peopleView(ctx)) : display(() => progressView(ctx));
    default: return display(() => progressView(ctx));
  }
}

async function render() {
  setNav();
  if (location.pathname === '/reset') { display(() => resetView(ctx)); return; }
  if (!isSignedIn()) { me = null; org = null; setNav(); display(() => signInView(ctx, begin)); return; }
  if (!me) { await begin(); return; }
  if (consent && consent.needs_consent) { display(() => consentView(ctx, consent, async () => { await ctx.refreshConsent(); render(); })); return; }
  if (!org) { display(() => noOrganisationView(me)); return; }
  if (location.pathname === '/device') { display(() => deviceView(ctx)); return; }
  route();
}

async function begin() {
  try {
    me = await api.get('/me');
    consent = await api.get('/privacy/consent');
  } catch (e) { me = null; return; }
  const active = me.memberships.filter((m) => m.status === 'active');
  org = active[0] || null;
  if (!org && me.memberships.length) ctx.say('Your access to the organisation has been switched off. Ask your administrator.', 'error');
  render();
}

setSignedOutHandler(() => { me = null; org = null; consent = null; if (location.pathname !== '/reset' && location.pathname !== '/device') history.replaceState(null, '', '/'); ctx.say('You are signed out.', 'ok'); render(); });
window.addEventListener('hashchange', () => {
  if (!isSignedIn() || !org) return;
  if (location.pathname !== '/') history.replaceState(null, '', '/' + location.hash);   // leaving /device for a hash page
  setNav(); route();
});
window.addEventListener('popstate', render);
loadConfig().then(render, () => display(() => h('section', {}, h('h1', { text: 'Not available' }), h('p', { class: 'notice', text: 'The service could not be reached. Try again in a moment.' }))));

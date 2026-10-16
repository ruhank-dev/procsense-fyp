// The pages. Each view gets a context { org, me, say(text, kind), go(route) } and returns an element to show. Everything shown from the database goes in as text (see dom.js).
import { api, ApiError, requestPasswordReset, setNewPassword, signIn, signOut } from './api.js';
import { barChart, lineChart } from './charts.js';
import { h, number, table, when } from './dom.js';

const PROVISIONAL = 'The process score uses provisional weights. The result (success or fail) does not depend on them.';
const base = (ctx) => `/organisations/${ctx.org.organisation_id}`;
const problem = (e) => (e instanceof ApiError ? (typeof e.detail === 'string' ? e.detail.replaceAll('_', ' ') : e.message) : 'Something went wrong.');
const guard = (ctx, fn) => async (...args) => { try { return await fn(...args); } catch (e) { ctx.say(problem(e), 'error'); return null; } };

function field(label, input) { return h('label', {}, label, input); }
function tile(value, label) { return h('div', { class: 'tile' }, h('div', { class: 'n', text: value }), h('div', { class: 'l', text: label })); }
function pill(text, kind) { return h('span', { class: `pill ${kind || ''}`, text }); }

// ---- signing in -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

export function signInView(ctx, after) {
  const email = h('input', { id: 'email', type: 'email', autocomplete: 'username', required: true });
  const password = h('input', { id: 'password', type: 'password', autocomplete: 'current-password', required: true });
  const form = h('form', { class: 'form-narrow', onsubmit: guard(ctx, async (event) => {
    event.preventDefault();
    await signIn(email.value.trim(), password.value);
    password.value = '';
    await after();
  }) },
  field('Email', email), field('Password', password), h('button', { type: 'submit', text: 'Sign in' }));
  return h('section', {}, h('h1', { text: 'Sign in' }), form,
    h('p', {}, h('a', { href: '/reset', text: 'Forgot your password?' })),
    h('p', { class: 'muted', text: 'Your organisation administrator invites you by email. Sign in with the address that received the invitation.' }));
}

export function resetView(ctx) {
  const hash = new URLSearchParams(location.hash.replace(/^#/, ''));
  const recovery = hash.get('type') === 'recovery' ? hash.get('access_token') : null;
  if (recovery) {
    history.replaceState(null, '', '/reset');   // the link's token leaves the address bar at once
    const password = h('input', { id: 'new-password', type: 'password', autocomplete: 'new-password', minlength: 8, required: true });
    return h('section', {}, h('h1', { text: 'Choose a new password' }),
      h('form', { class: 'form-narrow', onsubmit: guard(ctx, async (event) => {
        event.preventDefault();
        await setNewPassword(recovery, password.value);
        ctx.say('Your password was changed. You can sign in now.', 'ok');
        ctx.go('/');
      }) }, field('New password (at least 8 characters)', password), h('button', { type: 'submit', text: 'Save password' })));
  }
  const email = h('input', { id: 'reset-email', type: 'email', autocomplete: 'username', required: true });
  return h('section', {}, h('h1', { text: 'Reset your password' }),
    h('form', { class: 'form-narrow', onsubmit: guard(ctx, async (event) => {
      event.preventDefault();
      await requestPasswordReset(email.value.trim());
      ctx.say('If that address has an account, an email with a link is on its way.', 'ok');
    }) }, field('Email', email), h('button', { type: 'submit', text: 'Send the link' })),
    h('p', {}, h('a', { href: '/', text: 'Back to sign in' })));
}

export function noOrganisationView(me) {
  return h('section', {}, h('h1', { text: 'No organisation yet' }),
    h('p', { text: 'You are signed in, but you do not belong to an organisation yet.' }),
    h('p', { text: 'Ask your organisation administrator to invite the email address you signed in with. When you accept the invitation, your pages appear here.' }),
    me.is_platform_admin ? h('p', { class: 'muted', text: 'You are a platform administrator. Organisations are created with the admin tool.' }) : null);
}

// ---- progress ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

function summaryBlock(s) {
  const weekly = s.per_week.map((w) => ({ label: w.week.slice(5), value: w.sessions }));
  return [
    h('div', { class: 'tiles' }, tile(s.sessions, 'verified sessions'), tile(s.trainees, s.trainees === 1 ? 'person' : 'people'), tile(number(s.average_process_score), 'average process score (provisional)')),
    h('div', { class: 'charts' }, barChart('Sessions per week', weekly)),
    s.per_week.length ? h('details', {}, h('summary', { text: 'Sessions per week as a table' }), table(['Week starting', 'Sessions'], s.per_week.map((w) => [w.week, String(w.sessions)]))) : null,
    h('h2', { text: 'Results' }), s.results.length ? table(['Result', 'Sessions'], s.results.map((r) => [r.result || '—', String(r.count)])) : h('p', { class: 'muted', text: 'No verified sessions yet.' }),
    h('h2', { text: 'Critical items that came up most' }), s.critical_items.length ? table(['Item', 'Times'], s.critical_items.map((c) => [c.item_id, String(c.count)])) : h('p', { class: 'muted', text: 'None.' }),
    h('h2', { text: 'Behaviours that need work' }),
    s.behaviours.length ? table(['Behaviour', 'Sessions', 'Credits', 'Penalties', 'Net', 'Average net per session'], s.behaviours.map((b) => [b.behaviour, String(b.sessions), String(b.credits), String(b.penalties), String(b.net), number(b.average_net, 2)]), 'Lowest average first.') : h('p', { class: 'muted', text: 'No behaviour evidence yet.' }),
    h('p', { class: 'muted', text: PROVISIONAL }),
  ];
}

export async function progressView(ctx, userId) {
  const data = await api.get(userId ? `${base(ctx)}/members/${userId}/progress` : `${base(ctx)}/progress`);
  const series = data.series.filter((p) => p.process_score !== null).map((p) => ({ label: new Date(p.received_at).toLocaleDateString(), value: p.process_score }));
  return h('section', {}, h('h1', { text: userId ? `Progress of ${data.trainee}` : 'My progress' }), ...summaryBlock(data.summary),
    h('h2', { text: 'Process score over time' }), h('div', { class: 'charts' }, lineChart('Process score per session', series)),
    series.length ? h('details', {}, h('summary', { text: 'Scores as a table' }), table(['Date', 'Score'], series.map((p) => [p.label, String(p.value)]))) : null);
}

// ---- sessions and the report --------------------------------------------------------------------------------------------------------------------------------------------

export async function sessionsView(ctx, state = { offset: 0, mode: '', status: 'verified', result: '' }) {
  const limit = 25;
  const page = await api.get(`${base(ctx)}/sessions`, { limit, offset: state.offset, mode: state.mode, status: state.status, result: state.result });
  const rerender = (patch) => ctx.show(() => sessionsView(ctx, { ...state, offset: 0, ...patch }));
  const select = (id, label, options, current, key) => field(label, h('select', { id, onchange: (e) => rerender({ [key]: e.target.value }) }, options.map(([v, t]) => h('option', { value: v, selected: v === current, text: t }))));
  const supervisor = ctx.org.role !== 'trainee';
  const rows = page.items.map((s) => [when(s.received_at), supervisor ? (s.display_name ? `${s.display_name} (${s.trainee})` : s.trainee) : null, s.case_id, s.mode, pill(s.status, s.status === 'verified' ? 'good' : s.status === 'pending' ? '' : 'bad'),
    s.result || '—', number(s.process_score),
    s.status === 'verified' ? h('a', { href: `#/report/${s.session_id}`, text: 'Open report' }) : '']);
  const headers = ['Received', supervisor ? 'Person' : null, 'Case', 'Mode', 'Status', 'Result', 'Score (provisional)', ''];
  const keep = headers.map((x, i) => x !== null);
  return h('section', {}, h('h1', { text: supervisor ? 'Sessions in the organisation' : 'My sessions' }),
    h('div', { class: 'row' },
      select('f-status', 'Status', [['verified', 'Verified'], ['pending', 'Waiting for verification'], ['rejected', 'Rejected'], ['unverifiable', 'Could not be verified'], ['any', 'Any']], state.status, 'status'),
      select('f-mode', 'Mode', [['', 'Any'], ['practice', 'Practice'], ['assessment', 'Assessment']], state.mode, 'mode'),
      select('f-result', 'Result', [['', 'Any'], ['SUCCESS', 'Success'], ['SUCCESS_WITH_DELAY', 'Success with delay'], ['FAIL', 'Fail']], state.result, 'result')),
    page.items.length ? table(headers.filter((x, i) => keep[i]), rows.map((r) => r.filter((x, i) => keep[i]))) : h('p', { class: 'muted', text: 'No sessions match.' }),
    h('div', { class: 'row' },
      h('button', { class: 'secondary', disabled: state.offset === 0, text: 'Previous', onclick: () => ctx.show(() => sessionsView(ctx, { ...state, offset: Math.max(0, state.offset - limit) })) }),
      h('span', { class: 'muted', text: `${page.total === 0 ? 0 : state.offset + 1}–${Math.min(page.total, state.offset + limit)} of ${page.total}` }),
      h('button', { class: 'secondary', disabled: state.offset + limit >= page.total, text: 'Next', onclick: () => ctx.show(() => sessionsView(ctx, { ...state, offset: state.offset + limit })) })));
}

export async function reportView(ctx, sessionId) {
  const report = await api.get(`${base(ctx)}/sessions/${sessionId}/report`);
  const page = await api.getText(`${base(ctx)}/sessions/${sessionId}/report.html`).catch(() => null);   // one after the other: the access log records one look, not two at once
  const frame = page === null ? h('p', { class: 'muted', text: 'The full report page is not available for this session.' })
    : h('iframe', { class: 'report', title: 'Session report', sandbox: '', srcdoc: page });   // sandbox with no permissions: the report cannot run script or reach the rest of the page
  return h('section', {}, h('p', {}, h('a', { href: '#/sessions', text: '← Back to sessions' })),
    h('h1', { text: `Report: ${report.case_id}` }),
    h('div', { class: 'tiles' }, tile(report.result || '—', 'result'), tile(report.end_state || '—', 'end state'), tile(number(report.process_score), 'process score (provisional)'), tile(report.mode, 'mode')),
    h('p', { class: 'muted', text: `Person ${report.trainee}. Content ${report.content_version}, engine ${report.engine_version}. ${PROVISIONAL}` }), frame);
}

// ---- overview (supervisors) -------------------------------------------------------------------------------------------------------------------------------------------------------

export async function overviewView(ctx, cohortId = '') {
  const [cohorts, s] = await Promise.all([api.get(`${base(ctx)}/cohorts`), api.get(`${base(ctx)}/overview`, { cohort_id: cohortId })]);
  const pick = field('Cohort', h('select', { id: 'cohort', onchange: (e) => ctx.show(() => overviewView(ctx, e.target.value)) },
    h('option', { value: '', text: 'Whole organisation' }), cohorts.map((c) => h('option', { value: c.id, selected: c.id === cohortId, text: c.name }))));
  return h('section', {}, h('h1', { text: `Overview of ${ctx.org.organisation_name}` }), h('div', { class: 'row' }, pick), ...summaryBlock(s));
}

// ---- people (organisation administrators) -------------------------------------------------------------------------------------------------------------------------------------

export async function peopleView(ctx) {
  const [members, invitations, cohorts] = await Promise.all([api.get(`${base(ctx)}/members`), api.get(`${base(ctx)}/invitations`), api.get(`${base(ctx)}/cohorts`)]);
  const refresh = () => ctx.show(() => peopleView(ctx));
  const cohortName = (id) => (cohorts.find((c) => c.id === id) || { name: '?' }).name;
  const roleSelect = (m) => h('select', { 'aria-label': `Role of ${m.pseudonym}`, onchange: guard(ctx, async (e) => { await api.patch(`${base(ctx)}/members/${m.user_id}`, { role: e.target.value }); ctx.say('Role changed.', 'ok'); await refresh(); }) },
    ['trainee', 'supervisor', 'org_admin'].map((r) => h('option', { value: r, selected: r === m.role, text: r.replace('_', ' ') })));
  const toggle = (m) => h('button', { class: m.status === 'active' ? 'danger' : 'secondary', text: m.status === 'active' ? 'Deactivate' : 'Reactivate',
    onclick: guard(ctx, async () => { await api.patch(`${base(ctx)}/members/${m.user_id}`, { status: m.status === 'active' ? 'deactivated' : 'active' }); await refresh(); }) });
  const rows = members.map((m) => [m.display_name || '—', m.pseudonym, roleSelect(m), pill(m.status, m.status === 'active' ? 'good' : 'bad'), m.cohorts.map(cohortName).join(', ') || '—',
    h('span', {}, h('a', { href: `#/person/${m.user_id}`, text: 'Progress' }), ' ', toggle(m))]);

  const email = h('input', { id: 'invite-email', type: 'email', required: true });
  const role = h('select', { id: 'invite-role' }, ['trainee', 'supervisor', 'org_admin'].map((r) => h('option', { value: r, text: r.replace('_', ' ') })));
  const cohort = h('select', { id: 'invite-cohort' }, h('option', { value: '', text: 'No cohort' }), cohorts.map((c) => h('option', { value: c.id, text: c.name })));
  const invite = h('form', { class: 'row', onsubmit: guard(ctx, async (event) => {
    event.preventDefault();
    const sent = await api.post(`${base(ctx)}/invitations`, { email: email.value.trim(), role: role.value, cohort_id: cohort.value || null });
    ctx.say(sent.mail === 'sent' ? 'Invitation emailed.' : sent.mail === 'already_registered' ? 'That person already has an account; the invitation applies when they next sign in.' : 'The invitation was saved but the email could not be sent. It applies when that address signs in.', sent.mail === 'not_sent' ? 'error' : 'ok');
    await refresh();
  }) }, field('Email', email), field('Role', role), field('Cohort', cohort), h('button', { type: 'submit', text: 'Invite' }));

  const cohortName2 = h('input', { id: 'cohort-name', maxlength: 80, required: true });
  const addCohort = h('form', { class: 'row', onsubmit: guard(ctx, async (event) => { event.preventDefault(); await api.post(`${base(ctx)}/cohorts`, { name: cohortName2.value.trim() }); await refresh(); }) },
    field('New cohort', cohortName2), h('button', { type: 'submit', text: 'Create cohort' }));
  const active = members.filter((m) => m.status === 'active');
  const cohortBlocks = cohorts.map((c) => {
    const person = h('select', { 'aria-label': `Add a person to ${c.name}` }, active.map((m) => h('option', { value: m.user_id, text: `${m.display_name || m.pseudonym} (${m.role.replace('_', ' ')})` })));
    const add = (cohortRole) => guard(ctx, async () => { await api.put(`${base(ctx)}/cohorts/${c.id}/members/${person.value}`, { role: cohortRole }); await refresh(); });
    return h('div', { class: 'card' }, h('h3', { text: c.name }),
      c.members.length ? h('ul', {}, c.members.map((cm) => { const m = members.find((x) => x.user_id === cm.user_id); return h('li', {}, `${m ? (m.display_name || m.pseudonym) : cm.user_id} — ${cm.role} `,
        h('button', { class: 'secondary', text: 'Remove', onclick: guard(ctx, async () => { await api.del(`${base(ctx)}/cohorts/${c.id}/members/${cm.user_id}`); await refresh(); }) })); })) : h('p', { class: 'muted', text: 'Nobody yet.' }),
      h('div', { class: 'row' }, person, h('button', { class: 'secondary', text: 'Add as trainee', onclick: add('trainee') }), h('button', { class: 'secondary', text: 'Add as supervisor', onclick: add('supervisor') })));
  });

  return h('section', {}, h('h1', { text: 'People' }), h('h2', { text: 'Members' }),
    table(['Name', 'Id', 'Role', 'Status', 'Cohorts', ''], rows),
    h('h2', { text: 'Invite someone' }), invite,
    h('h2', { text: 'Waiting invitations' }),
    invitations.length ? table(['Email', 'Role', 'Expires', ''], invitations.map((i) => [i.email || '—', i.role.replace('_', ' '), when(i.expires_at),
      h('button', { class: 'secondary', text: 'Cancel', onclick: guard(ctx, async () => { await api.del(`${base(ctx)}/invitations/${i.id}`); await refresh(); }) })])) : h('p', { class: 'muted', text: 'None.' }),
    h('h2', { text: 'Cohorts' }), addCohort, ...cohortBlocks);
}

// ---- headset sign-in ---------------------------------------------------------------------------------------------------------------------------------------------------------------

export function deviceView(ctx) {
  const given = new URLSearchParams(location.search).get('code') || '';
  const code = h('input', { id: 'device-code', value: given, autocomplete: 'off', autocapitalize: 'characters', spellcheck: 'false', maxlength: 12, required: true, 'aria-describedby': 'code-help' });
  const result = h('div', { 'aria-live': 'polite' });
  const decide = (approve) => guard(ctx, async () => {
    const out = await api.post('/auth/device/decision', { user_code: code.value, approve });
    result.replaceChildren(h('p', { class: 'notice', text: approve ? `Approved. "${out.device_name}" is signing in as you. You can close this page.` : `Denied. "${out.device_name}" was not signed in.` }));
  });
  return h('section', {}, h('h1', { text: 'Sign in a headset' }),
    h('p', { text: 'Type the code the headset shows. Approve it only if you are the person who is putting the headset on, and you started this sign-in yourself.' }),
    h('div', { class: 'form-narrow' }, field('Code from the headset', code), h('p', { id: 'code-help', class: 'muted', text: 'Like BCDF-GHJK. Capitals or small letters both work.' }),
      h('div', { class: 'row' }, h('button', { text: 'Approve', onclick: decide(true) }), h('button', { class: 'danger', text: 'Deny', onclick: decide(false) }))), result);
}

// ---- consent and privacy ---------------------------------------------------------------------------------------------------------------------------------------------------------------

export function consentView(ctx, state, after) {
  const boxes = {};
  const blocks = state.kinds.map((k) => {
    boxes[k.kind] = h('input', { id: `consent-${k.kind}`, type: 'checkbox', checked: k.granted === true && k.current });
    return h('div', { class: 'card' }, h('h2', { text: k.title }), h('p', { text: k.text }),
      h('label', { class: 'inline' }, boxes[k.kind], k.required ? ' I agree (needed to store my sessions)' : ' I agree (optional)'));
  });
  return h('section', {}, h('h1', { text: 'Your data and your choices' }),
    h('p', { text: 'Please read this before you continue. You can change your choices later on the Privacy page.' }), ...blocks,
    h('div', { class: 'row' }, h('button', { text: 'Save my choices', onclick: guard(ctx, async () => {
      for (const k of state.kinds) {
        const now = boxes[k.kind].checked;
        if (k.required && !now) { ctx.say('You can use ProcSense only if you agree to the first one: otherwise your sessions cannot be stored.', 'error'); return; }
        if (now !== (k.granted === true && k.current) || (k.required && !k.current)) await api.post('/privacy/consent', { kind: k.kind, granted: now });
      }
      ctx.say('Your choices are saved.', 'ok');
      await after();
    }) })));
}

async function downloadMyData(ctx) {
  const text = await api.getText('/privacy/export');
  const url = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const link = h('a', { href: url, download: 'my-procsense-data.json' });
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
  ctx.say('Your data was downloaded.', 'ok');
}

const WHAT = { view_report: 'Opened a session report', view_progress: 'Opened my progress', view_sessions: 'Opened my session list', export: 'Exported results that include me', view_ai_calls: 'Opened what the AI did for me' };

export async function privacyView(ctx, consentState) {
  const [entries, retention] = await Promise.all([api.get('/privacy/access-log'), ctx.org.role === 'org_admin' ? api.get(`${base(ctx)}/retention`) : Promise.resolve(null)]);
  const confirm = h('input', { id: 'delete-confirm', autocomplete: 'off', 'aria-describedby': 'delete-help' });
  const months = retention ? h('input', { id: 'retention', type: 'number', min: 6, max: 60, value: retention.retention_months ?? '', placeholder: String(retention.default_months) }) : null;
  const change = () => ctx.show(() => consentView(ctx, consentState, async () => { await ctx.refreshConsent(); ctx.go('#/privacy'); }));
  return h('section', {}, h('h1', { text: 'Privacy' }),
    h('h2', { text: 'My choices' }), table(['Question', 'My answer'], consentState.kinds.map((k) => [k.title, k.granted === null ? 'Not answered' : (k.granted ? 'Yes' : 'No')])),
    h('div', { class: 'row' }, h('button', { class: 'secondary', text: 'Change my choices', onclick: change })),
    h('h2', { text: 'Who looked at my data' }),
    entries.length ? table(['When', 'Who', 'Role', 'What', 'Organisation'], entries.map((e) => [when(e.at), e.viewer, e.viewer_role.replace('_', ' '), WHAT[e.action] || e.action, e.organisation]),
      'Supervisors and administrators of my organisations. Totals over many people are not listed.') : h('p', { class: 'muted', text: 'Nobody has opened your results one by one.' }),
    h('h2', { text: 'Download my data' }), h('p', { text: 'Everything ProcSense holds about you, in one file. The stored recordings of your sessions are not in it; ask your administrator if you need them.' }),
    h('div', { class: 'row' }, h('button', { text: 'Download my data', onclick: guard(ctx, () => downloadMyData(ctx)) })),
    retention ? [h('h2', { text: 'How long this organisation keeps session recordings' }),
      h('p', { class: 'muted', text: `After this time the stored recording and report page of a session are removed; the results and totals stay. Empty means the platform's ${retention.default_months} months. Allowed: 6 to 60.` }),
      h('div', { class: 'row' }, field('Months', months), h('button', { text: 'Save', onclick: guard(ctx, async () => { await api.put(`${base(ctx)}/retention`, { retention_months: months.value === '' ? null : Number(months.value) }); ctx.say('Saved.', 'ok'); }) }))] : null,
    h('h2', { text: 'Delete my data' }),
    h('p', { text: "This deletes your sign-in account and cuts every link between you and your sessions. Your name, your email address and your id are gone. Your sessions stay in your organisation's totals without any link to you, so the numbers do not change. It cannot be undone." }),
    h('p', { id: 'delete-help', class: 'muted', text: 'If you are the only administrator of an organisation, hand that over first.' }),
    h('div', { class: 'row' }, field('Type DELETE to confirm', confirm), h('button', { class: 'danger', text: 'Delete my data', onclick: guard(ctx, async () => {
      await api.post('/privacy/delete-my-data', { confirm: confirm.value });
      signOut();
      ctx.say('Your data was deleted and you were signed out. Thank you for using ProcSense.', 'ok');
    }) })));
}

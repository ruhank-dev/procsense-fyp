# p1-backend-s11 — Web dashboard

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
The pages people use in a browser, served by the same FastAPI service: sign-in and password reset, a trainee's own progress, the organisation overview, cohorts, a session report viewer, people management for organisation admins (invite, role, cohort, deactivate), the device-approval page for headset login (`p1-backend-s10`), and the consent page (`p1-backend-s12`). Static HTML and plain JavaScript call the JSON API from `p1-backend-s04`; charts use a bundled chart library; there is no separate front-end project and no build step.

## Why
A supervisor must see results across trainees and sites without installing anything, and the headset login needs a web page to approve it. One deployment keeps the free hosting to a single service and keeps the first version small.

## How to implement
1. `server/static/` with a small set of pages and modules (sign-in, progress, overview, cohort, people, report, device, consent), a shared navigation that shows only what the person's role allows, and a bundled chart library file (not loaded from another site).
2. Sign-in with the account system's web sign-in (email and password, reset by email); the page keeps the access token in memory and refreshes it, and sends it as a bearer token to the API. No token in local storage.
3. Security headers on every page (a strict content-security policy, no inline scripts, frame protection), cross-site request protection for any cookie, output escaping everywhere data from users is shown (display names, case ids, free-text reasons).
4. Pages are readable on a phone and usable by keyboard; text sizes and colours meet the project's accessibility checklist; the report viewer embeds the session report page.
5. A person with no organisation sees an explanation, not an error; a deactivated person is signed out.
6. Tests: the pages are served and carry the headers; the navigation differs by role; an automated browser smoke test (sign in, open the overview, open a report, sign out) runs in CI against a local server with test tokens; a manual checklist for the rest.

## Verification
A supervisor sees their organisation and cannot reach another by changing an address (T-BE5 through the browser); a trainee sees only their own pages; the smoke test passes; the headset device-code approval works end to end with the Quest or its simulator.

## Files changed
`server/static/*`, `server/routers/pages.py`, `server/middleware/security_headers.py`, tests including the browser smoke test, `docs/CI.md`.

## Dependencies
p1-backend-s04, p1-backend-s08, p1-backend-s10 (device page).

## Common issues
A script loaded from another site or an inline script defeats the content-security policy → bundle everything. Showing a name or reason without escaping is a script-injection hole → escape on output and test it with hostile strings. If supervisors later want interactive analysis, a separate front end is possible without changing the API (DT-128).

## Sources
ADR-0009; the project's accessibility checklist (walkthrough Part I); OWASP guidance for web pages.

## Implementation notes (14 Oct 2026)
- **What was built:** `server/static/` (`index.html`, `css/app.css`, `js/{app,api,dom,views,charts}.js`), `server/routers/pages.py` (`/`, `/device`, `/reset`, `/config.json`, and `/static`), `server/middleware/security_headers.py`. Views: sign in, forgot and reset password, no-organisation explanation, my progress, sessions list with filters and paging, report viewer, overview with a cohort picker (supervisors), person progress, people (organisation administrators: members with role and deactivate, invite, waiting invitations, cohorts with members), and the headset code approval page. The navigation shows only what the role allows; the same API still refuses what the page hides.
- **Decisions that are the project team's, not the book's:** (1) one page with `#/` routes, so the access token can stay in memory while moving between views (separate pages would lose it on every click); a reload therefore signs the person out (DT-149; a refresh cookie set by the server is the fix); (2) our own 100-line SVG charts instead of a bundled chart library: nothing to download, update or allow through the policy, and each chart comes with a description and a table of the same numbers; (3) the sign-in goes straight from the page to the account system (`/auth/v1/token`, `/recover`, `/user`) with the publishable key, so the service never sees a password; (4) text from the database is only ever put in the page as text (`dom.js` has no markup path, and a test fails the build if `innerHTML`, `eval`, inline handlers, storage or another site's address appear in `static/`); (5) the policy allows inline *styles* (not scripts) because the session report is shown in a frame with no permissions (`sandbox=""`) made from the report's own page, which carries its style inline; (6) the headset approval page asks the person to approve only if they started the sign-in themselves (a code approved for a stranger signs the stranger in as the approver); (7) a deactivated member is signed out when the service answers `membership_deactivated`.
- **Not done:** the consent page (it belongs to p1-backend-s12, which defines what the person agrees to); the password reset and invitation emails open the right page only after the Supabase redirect addresses are set (DT-150); the accessibility check by a person with a screen reader (the manual list in `server/RUNBOOK.md`, DT-151); the headset flow with a real Quest (the page and the API are tested end to end in a browser, the headset side is Sannan's, DT-144).
- **Tests:** 14 in `server/tests/test_pages.py` (served, headers on every response, HSTS in production, the public config never holds the secret, static scan) and one browser test, `test_browser_smoke.py`, which runs the service on a local port with `server/tests/browser_stack.py` (pretend sign-in, sample data, hostile names) in a real browser: Edge locally (`BROWSER_CHANNEL`, `task ci` sets it on Windows), Chromium in the CI job. It was also run by hand in the app's browser pane.


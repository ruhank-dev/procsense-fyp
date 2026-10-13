# p1-backend-s08 — Organisations, roles and accounts (Supabase Auth)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Who is who and who may see what. Accounts live in Supabase Auth; the service verifies the login token on every request and looks up the person's organisation, role and cohorts in its own tables. Roles: trainee, supervisor, organisation admin; a platform administrator (the team) is set directly in the database. Organisations invite people; supervisors look after cohorts.

## Why
The product is sold to organisations. Every query and every rule must know whose data it is touching, and nobody may see another organisation's data. Keeping roles in our own tables (not only in the token) lets a removed person lose access immediately and lets tests cover every case.

## How to implement
1. Tables (Alembic): `organisation`, `profile` (the Supabase user id, a pseudonymous id such as `T-` plus random characters, a display name, created time; the email stays in Supabase Auth only), `membership` (user, organisation, role, status active or deactivated), `cohort`, `cohort_member`, `consent_record` (filled by `s12`). Every table has the organisation where it belongs; constraints make the wrong state impossible (one active membership per user and organisation, a role from a fixed list).
2. Token verification: fetch and cache the project's public key set, verify the signature, issuer, audience and expiry (a few seconds of clock skew allowed); accept the algorithm Supabase signs with and refuse `none`; a FastAPI dependency `current_user` returns the user and creates the profile on first sign-in.
3. Authorisation dependencies: `require_member(organisation)`, `require_role(role)` and a helper that scopes every repository call to an organisation. Membership and status are read from the database on each request (cached for seconds only), never trusted from the token.
4. Endpoints: `GET /me` (profile, memberships), `POST /organisations` and the first organisation admin (platform administrator only, through a script `server/scripts/create_organisation.py` first), `POST /organisations/{id}/invitations` (organisation admin; calls Supabase's invitation function with the server-side key), `GET/POST /organisations/{id}/cohorts`, `PATCH /organisations/{id}/members/{user}` (role, cohort, deactivate); members are deactivated, never deleted.
5. Row-level security enabled on every table with no policies for the public roles, as a second lock (a test connects with the public role and sees nothing).
6. Pseudonymous ids are what session data and exports carry; the mapping to a display name is visible only to supervisors and admins of the same organisation.

## Verification
T-AC1 (a role-by-endpoint matrix over trainee, supervisor, organisation admin, a member of another organisation and no login), T-AC2 (an expired, forged, wrong-audience or unsigned token is refused), T-AC3 (a deactivated member loses access on the next request), T-AC4 (an invitation creates an account that lands in the right organisation and role), T-AC5 (the public database role reads nothing) pass. Two organisations are used in every test.

## Files changed
`server/models/{organisation,profile,membership,cohort}.py`, `server/auth/{tokens,dependencies}.py`, `server/routers/{me,organisations,cohorts}.py`, `server/repositories/*`, `server/schemas/*`, `server/scripts/create_organisation.py`, Alembic migration, tests.

## Dependencies
p1-backend-s01, p1-backend-s07.

## Common issues
Trusting a role claim in the token means a removed person keeps access until it expires → check the database. A person in two organisations needs an explicit active organisation per request. The server-side key can do anything → only the invitation code uses it, and it is never logged. Tests cannot call a real Supabase for every run → sign test tokens with a local key and serve a test key set, and keep one slower test against the development project, run by hand.

## Sources
ADR-0009; Supabase Auth documentation (token verification, invitations); INSTRUCTIONS §4b, §5.

## Implementation notes (13 Oct 2026)
- **Tables** (`server/models/accounts.py`, migration `0002`): `organisation`, `profile` (the Supabase user id, a pseudonymous id `T-` plus 8 easy-to-read characters, a display name, a platform-administrator flag; no email), `membership` (role and status checked by the database), `cohort`, `cohort_member`, `consent_record` (filled by s12) and `invitation` (added here: it holds the invited address only until the invitation is accepted, revoked or expired, then the address is cleared). Row-level security is switched on for all seven with no policies, and the rights Supabase gives `anon` and `authenticated` by default are taken away (skipped on a plain PostgreSQL, which has no such roles).
- **Login tokens** (`server/auth/tokens.py`): checked against the project's published keys (the real project publishes one ES256 P-256 key, confirmed on 13 Oct 2026); only ES256 and RS256 are accepted, so `none` and a token signed with the public key as an HMAC secret are refused; issuer, audience, expiry (10 s of clock leeway), required claims and a UUID subject are checked; the key set is cached 10 minutes and refreshed once when a token names an unknown key, at most once a minute.
- **Who may do what** (`server/auth/dependencies.py`): the role and the active status are read from the database on every request. No token or not valid = 401 with a plain body; not a member = 404; a member without the role or a deactivated member = 403. A platform administrator acts as an administrator in every organisation (the team; DT-129 and the access log of s12 cover its audit).
- **Endpoints:** `GET` and `PATCH /me`; `POST /organisations` (platform administrator: creates it and invites the first administrator); `GET /organisations/{id}`; members (`GET`, `PATCH`: role, deactivate, never delete, the last administrator is protected); invitations (`GET`, `POST`, `DELETE`); cohorts (`GET`, `POST`, `PUT` and `DELETE` of members). Invitations are applied when the invited address signs in (the address in the token), once, and an expired or revoked one is ignored; a failed email does not fail the invitation.
- **Supabase's secret key** is used in one place only (`server/auth/supabase_admin.py`, the invitation email), sent in the `apikey` header only (the new secret keys are not tokens). Whether Supabase's invitation call accepts the new key style has not been proven against the real project (DT-138): the fake sender is tested; the first real invitation will show it.
- **Platform administration** (`server/scripts/admin.py`, `task server-admin`): create an organisation with its first administrator, and grant platform-administrator rights. No screen (DT-129).
- **Tests:** 47 new (69 in all), on a real PostgreSQL: the role matrix over nine endpoints and eight kinds of caller (T-AC1), tokens at the door (T-AC2) and in detail, deactivation at once and the role never taken from the token (T-AC3), invitations (T-AC4), row-level security with a role standing in for Supabase's (T-AC5, with a control that shows the check would notice if the lock were off), isolation between two organisations, cohort rules, the database refusing a bad role, the admin script, and an optional check of the real project's key set (`LIVE_SUPABASE_URL`).
- **Not in this step:** the sessions and their upload (s02), the headset sign-in (s10), the consent and delete-my-data work (s12), a screen for platform administration (DT-129).


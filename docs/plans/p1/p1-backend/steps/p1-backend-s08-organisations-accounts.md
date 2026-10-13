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

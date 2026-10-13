# p1-backend-s07 — Supabase and hosting setup (environments, secrets, runbook)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Create and configure the free accounts and the places the service runs: a Supabase organisation with a development project (the production project is created later: the free plan allows a person two active projects in total, and Moiz already has two), a free container host for the FastAPI service, the secrets in the right places (never in git), and a short runbook so any team member can find, reach and recover each part. No application code is written in this step.

## Why
Everything else in the backend stands on these accounts and keys. Doing it first, once, on purpose, with the rules about secrets written down, avoids a key pasted into a chat or committed by accident, and removes the "who owns the account" question before there is data in it.

## How to implement
1. Decide and record the account owner and a second administrator for every account (Supabase, the container host, GitHub Actions secrets). Use a shared team email for the accounts, with each person added as a member rather than sharing one password.
2. Supabase: one organisation and the project `procsense-dev` (`procsense-prod` waits for a paid plan, DT-123 and DT-133; until then no real trainee data goes into the one project), the region nearest the users and the same region as the container host (check availability when creating; record it, because it is where the data lives). Record the PostgreSQL major version.
3. Supabase settings: the data API off or limited to nothing (the service is the only client of the database), email confirmation on, password rules on, row-level security enabled by default on new tables. Public sign-up stays on in the development project for testing and is switched off in production (accounts are created by invitation, `p1-backend-s08`).
3b. A custom email sender. Supabase's built-in email sends 2 messages an hour and only to members of the Supabase team, and invitations and password sign-up need a custom sender. For development: Gmail SMTP with an app password (host `smtp.gmail.com`, port 465; about 500 messages a day; it can land in spam). Before real customers: a sender on the team's own domain through a transactional provider with SPF and DKIM (DT-132). Send a test invitation to an address outside the team.
4. Collect, for the project: the project URL, the public key (it is public by design), the server-side key (secret, never in the headset or the web pages), the database password, the pooler connection strings for the application (transaction mode) and for migrations (session mode), and the address of the key set used to verify login tokens.
5. Container host (first choice: Render's free web service, Docker, same region): the account now, the service at the end of `p1-backend-s01` when `server/Dockerfile` exists. Health check path `/health`, automatic deploys switched off (a deploy happens from CI after the checks pass, `p1-backend-s13`), environment variables filled from step 4. The code reaches Render as an image built and tested in CI and published to GitHub's container registry (Render pulls it with a read-only token), so the image that passed the checks is the one that runs and no Render build minutes are used; Render's own GitHub connection is the fallback.
6. Secrets: environment variables on the host, repository secrets in GitHub for CI and the scheduled jobs, a git-ignored `.env` on laptops; `server/.env.example` lists every variable with a fake value; a CI check fails if a file in git contains a string shaped like a key. A leaked key is rotated, not just deleted from git.
7. `server/RUNBOOK.md`: who owns what, where each secret lives, how to reach the database (`psql` through the pooler), how to read logs, what to do when the project is paused or the service is asleep, how to rotate each key.
8. Record the decision and the limits that matter (database size about 500 MB, pausing after a week of inactivity, no automatic backups, connection limits; the host sleeps when idle) in the runbook and in `p1-backend` README with the date, because free plans change.

## Verification
From a clean laptop, a second team member signs in to the Supabase and host dashboards; a test invitation reaches an address outside the team; `curl https://<service>/health` answers over HTTPS once the skeleton (`s01`) is deployed; a query through the session pooler reaches `procsense-dev`; the secret scan passes in CI and refuses a pasted key (`tools/ci/test_repo_checks.py`); the runbook is followed by someone who did not write it.

## Files changed
`server/RUNBOOK.md`, `server/.env.example`, `tools/ci/repo_checks.py` (the Supabase key, database address and token patterns), `tools/ci/test_repo_checks.py`, `tools/ci/run_all.py`, `.github/workflows/ci.yml`, `docs/CI.md`, `docs/plans/p1/p1-backend/README.md` (region, versions, limits).

## Dependencies
p0-foundation-s01 (the remote repository and secrets handling).

## Implementation notes (13 Oct 2026)
- **Done:** the Supabase organisation `Procsense` and the project `procsense-dev` (Singapore, PostgreSQL 17.11) with the keys in the password manager; custom email through Gmail, test invitation received; `server/RUNBOOK.md` and `server/.env.example`; the secret scan extended and tested (`tools/ci/test_repo_checks.py`, step 7ag in `docs/CI.md`).
- **Not done yet, and why:** the second project `procsense-prod` (the free plan's limit of two active projects per person; DT-133); Ruhan and Sannan have been invited and have not accepted yet; the Render account and service (the service needs the Dockerfile of `p1-backend-s01`).
- **Found while doing it:** Supabase's built-in email cannot send invitations to outsiders (added step 3b and DT-132); the free-project limit counts a person's projects across all organisations they administer.

## Common issues
The free plan's direct database address is IPv6-only → use the pooler addresses. A server-side key in the headset or in a web page gives anyone full access → it exists only on the host and in CI. Free plans change their limits → recheck before relying on a number and write the date.

## Sources
ADR-0009; Supabase and host documentation (record the pages used and the date in the runbook); INSTRUCTIONS §5 (no credentials in code).

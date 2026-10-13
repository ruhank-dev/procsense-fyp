# p1-backend-s07 — Supabase and hosting setup (environments, secrets, runbook)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Create and configure the free accounts and the places the service runs: a Supabase organisation with a development project and a production project (the free plan allows two projects), a free container host for the FastAPI service, the secrets in the right places (never in git), and a short runbook so any team member can find, reach and recover each part. No application code is written in this step.

## Why
Everything else in the backend stands on these accounts and keys. Doing it first, once, on purpose, with the rules about secrets written down, avoids a key pasted into a chat or committed by accident, and removes the "who owns the account" question before there is data in it.

## How to implement
1. Decide and record the account owner and a second administrator for every account (Supabase, the container host, GitHub Actions secrets). Use a shared team email for the accounts, with each person added as a member rather than sharing one password.
2. Supabase: one organisation, projects `procsense-dev` and `procsense-prod`, the region nearest the users and the same region as the container host (check availability when creating; record it, because it is where the data lives). Record the PostgreSQL major version.
3. Supabase settings for production: public sign-up off (accounts are created by invitation, `p1-backend-s08`), email confirmation on, password rules on, the data API switched off or limited to nothing (the service is the only client of the database), row-level security enabled by default on new tables.
4. Collect, for each project: the project URL, the public key (it is public by design), the server-side key (secret, never in the headset or the web pages), the database password, the pooler connection strings for the application (transaction mode) and for migrations (session mode), and the address of the key set used to verify login tokens.
5. Container host (first choice: Render's free web service, Docker, same region): a service from `server/Dockerfile`, health check path `/health`, automatic deploys switched off (a deploy happens from CI after the checks pass, `p1-backend-s13`), environment variables filled from step 4.
6. Secrets: environment variables on the host, repository secrets in GitHub for CI and the scheduled jobs, a git-ignored `.env` on laptops; `server/.env.example` lists every variable with a fake value; a CI check fails if a file in git contains a string shaped like a key. A leaked key is rotated, not just deleted from git.
7. `server/RUNBOOK.md`: who owns what, where each secret lives, how to reach the database (`psql` through the pooler), how to read logs, what to do when the project is paused or the service is asleep, how to rotate each key.
8. Record the decision and the limits that matter (database size about 500 MB, pausing after a week of inactivity, no automatic backups, connection limits; the host sleeps when idle) in the runbook and in `p1-backend` README with the date, because free plans change.

## Verification
From a clean laptop, a second team member signs in to both dashboards; `curl https://<service>/health` answers over HTTPS once the skeleton (`s01`) is deployed; `psql` through the pooler reaches `procsense-dev`; the secret scan passes in CI; the runbook is followed by someone who did not write it.

## Files changed
`server/RUNBOOK.md`, `server/.env.example`, `.github/workflows/ci.yml` (secret scan), `docs/CI.md`, `docs/plans/p1/p1-backend/README.md` (region, versions, limits).

## Dependencies
p0-foundation-s01 (the remote repository and secrets handling).

## Common issues
The free plan's direct database address is IPv6-only → use the pooler addresses. A server-side key in the headset or in a web page gives anyone full access → it exists only on the host and in CI. Free plans change their limits → recheck before relying on a number and write the date.

## Sources
ADR-0009; Supabase and host documentation (record the pages used and the date in the runbook); INSTRUCTIONS §5 (no credentials in code).

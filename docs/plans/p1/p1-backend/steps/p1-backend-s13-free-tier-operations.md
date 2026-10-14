# p1-backend-s13 — Operations on the free tier: backups, keep-alive, deploy, monitoring (EXP-BE-3)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Everything that keeps a free-plan service trustworthy: automatic deploys only after the checks pass, a nightly encrypted backup of our tables with a tested restore, a keep-alive so the database is not paused for inactivity, a wake-up sweep for the verification queue, uptime and size monitoring that opens an issue before something fills up, and a written "when to leave the free plan" list. EXP-BE-3 measures whether the free tier holds a realistic load.

## Why
The free plans have no automatic backups, pause a quiet database after a week and put an idle service to sleep. Each is harmless if it is planned for and a disaster during a demonstration if it is not. A sold product also needs to prove it can recover.

## How to implement
1. Deploy: after CI is green on `main`, a workflow calls the host's deploy hook with a repository secret; `GET /health` is checked afterwards and a failed check opens an issue. Database migrations run as a separate, logged step before the new version starts, and every migration has a way back.
2. Backups: a scheduled GitHub Actions workflow dumps the application tables (not the account system's own data) through the migration connection, encrypts the dump with a key held outside the repository, and keeps it as a private artifact for 14 days; it fails loudly if the dump is empty or smaller than the last by a large margin. A restore drill into a scratch project is written in the runbook and run once, then every quarter. Accounts: the account system's users are not in this dump; the runbook says what happens if the project is lost (people are re-invited, sessions are restored from the dump).
3. Keep-alive: a scheduled call every few days to `/health/db` so the database never sits idle for a week; a second call wakes the service and runs the verification sweeper (`p1-backend-s09`).
4. Monitoring: an uptime check (a scheduled workflow or a free monitoring service) on `/health`; a daily check of database size and connection count that opens an issue at 80 % of the plan's limit; error reports without personal data.
5. Secrets rotation: how and when each key is rotated, written in the runbook and rehearsed once.
6. "When to leave the free plan": the list of signals (database over 400 MB, cold starts hurting a demonstration, a customer who needs backups with guarantees, any real customer data) and the first paid step for each; recorded in DT-123.
7. EXP-BE-3: load 1,000 synthetic sessions across 5 organisations, measure database size, every dashboard query, the wake-up time of the service and a pause-and-restore drill; record the numbers and the date in the README.

## Verification
EXP-BE-3 meets its metric (database under 400 MB; dashboard queries under 1 s; wake-up under 60 s; restore from our backup works); a deliberately failing migration does not take the service down; a backup older than 14 days is gone; a restore drill is documented with the time it took.

## Files changed
`.github/workflows/{deploy,backup,keepalive,monitor}.yml`, `server/scripts/{backup,restore,load_synthetic_sessions}.py`, `server/RUNBOOK.md`, `docs/CI.md`, `docs/plans/p1/p1-backend/README.md` (EXP-BE-3 result).

## Dependencies
p1-backend-s02, p1-backend-s07, p1-backend-s09.

## Common issues
Scheduled workflows on a private repository use the free allowance of minutes → keep them short and few. A backup nobody has restored is not a backup → the drill is part of the step. The free plan's limits change → recheck them and record the date.

## Sources
ADR-0009; Supabase and host documentation (record the pages and the date); Implementation Guide B-M16.

## Progress (14 Oct 2026): the deploy part is written; the rest of the step is not
- **Built:** `.github/workflows/publish-image.yml` (the image, built and checked on GitHub, pushed to the container registry; the host pulls it, because the Linux engine and the content versions are generated and not in git, so the host cannot build from the repository); `.github/workflows/scheduled-jobs.yml` (the wake-up and keep-alive call, the verification sweep every 3 hours, the retention clean-up daily); the image carries its commit (`/health` shows it); `server/scripts/headset_sim.py` (a stand-in for the headset: sign in with a code, upload, wait for the verdict, fetch progress; `task headset-sim`); `tools/demo/make_demo_session.py` (a session file from the real engine with the header the service verifies; `task demo-session`); `add-member` in the admin script; the deploy steps in `server/RUNBOOK.md` and `server/DEMO.md`.
- **Tried here:** the whole chain on this laptop with the real engine: a session file from `demo-session`, the stand-in signed in with a code approved on the web, the upload was accepted, the engine verified it (`SUCCESS`, `REDIRECTED_APPROPRIATELY`) and the person's progress came back with it.
- **Decisions that are the project team's, not the book's:** the image goes to GitHub's container registry and stays private, with a read-only token for the host (the plan's alternative was a deploy hook only); the image workflow runs on pushes to `myt1x54/backend-*` and main so the demonstration does not wait for a merge; scheduled jobs are every 3 hours and daily so they stay inside the free minutes of a private repository; the scheduled call also touches `/health/db`, which is the keep-alive for the free database (whether that is enough to stop the pause is checked by watching the project, DT-160).
- **Not done:** the nightly backup and a rehearsed restore, the uptime and size monitor, rotation rehearsal, the 1,000-session load test (EXP-BE-3), the "when to leave the free plan" list; the first run of the two workflows on GitHub (DT-157); the Render service itself, which the owner creates with the steps in the runbook (DT-158).
- **Found by the first publish-image run on GitHub (14 Oct 2026):** the container never answered `/health` because `server/models/` (the ORM tables) was **never committed**: `.gitignore` line `models/` (meant for machine-learning model files) matched it, so every backend branch from s01 to s13 on GitHub lacked the package and could not start; it passed everywhere on this laptop because the files existed on disk. Fixed with `!server/models/` in `.gitignore`, the five files added, and a new check in the project rules that fails when a source file is ignored by git (`tools/ci/repo_checks.py`). The earlier branches (s01 to s12) as pushed are incomplete on their own; the newest branch contains the whole chain (DT-161).
- **Found on the live host (14 Oct 2026):** the first session uploaded to the service stayed `pending`: the engine was stopped by the system (`exit -25`, signal SIGXFSZ) because the service runs it under a 64 MiB file-size limit and the .NET runtime's default write-xor-execute memory layout creates a much larger shared-memory file. It worked in every earlier test because they ran the engine without that limit. Fixed with `DOTNET_EnableWriteXorExecute=0` in the image (also faster to start); the log now names the signal; the image check in the publish workflow now runs the engine under the same limit as the service does.

## Continuous delivery (14 Oct 2026)
- **Built (branch myt1x54/cd-pipeline):** `.github/workflows/deploy.yml` (after CI is green on main: build and check the image for that commit, migrate, tell the host, wait until it runs the commit and its database answers, open an issue on failure), `publish-image.yml` made callable (`sha`, `latest` inputs; a backend branch build tags `branch-<name>` and never moves `latest`; the old unconditional deploy-hook step is gone), `tools/ci/wait_for_deploy.py` with six tests, a YAML check of every workflow in the project rules, the runbook section "Deploying and going back".
- **Decisions that are the project team's, not the book's:** the image that the host runs (`latest`) is only ever built from a commit of main whose CI was green (a branch build can no longer replace it); migrations run from the CI runner before the deploy (rather than in the container at start-up) and must be additive; the deploy is not gated by a manual approval (adding a GitHub environment with a required reviewer is one setting, DT-167); going back is a one-line image tag change in the host, not a workflow.
- **Not done:** the secrets (the owner adds them), the first real run of the Deploy workflow (it can only run after this branch is merged to main, because `workflow_run` workflows run from the default branch), a rehearsal of going back (DT-166), the nightly backup and monitor (DT-160).

## Backup and monitor (14 Oct 2026)
- **Built (branch myt1x54/backup):** `server/scripts/backup.py` (dump of the schema `public` from one consistent snapshot with row counts taken in the same snapshot, AES-256-GCM encryption in pieces so a wrong key, a changed byte or a cut-off file is refused, restore, throwaway-database drill, size monitor), `.github/workflows/backup.yml` (nightly: dump, encrypt, prove the restore, keep 14 days, fail and open an issue when the dump is empty, under half the size of the last or cannot be restored), `.github/workflows/monitor.yml` (daily size and connections, issue over 80% of 500 MB), `server/tests/test_backup.py` (7 tests, including a real dump restored into a throwaway PostgreSQL), runbook section "Backup and restore".
- **Proved on the live project (local run, key made for the run, files deleted afterwards):** 17 tables and 25 rows dumped in 24 s (49 KB encrypted) and restored into a throwaway PostgreSQL in 3.3 s with every table's count equal.
- **Decisions that are the project team's:** the backup covers only the application tables, not Supabase accounts (DT-130, DT-170); the encryption key is a repository secret the owner keeps a copy of (DT-168); artifacts are kept 14 days on GitHub only (DT-169); a backup under half the size of the last one fails the run so a person looks.
- **Not done:** the secret `BACKUP_KEY` and the first run on GitHub (owner), a second copy off GitHub, the load test and the exit list (DT-160).


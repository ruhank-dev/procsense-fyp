# Runbook: hosting the ProcSense backend (p1-backend-s07)

Written 13 October 2026. Free plans change their limits, so recheck the numbers below before relying on them and update the date. No secret appears in this file; secrets are in the team password manager (see "Where secrets live").

## What is running today
| Part | State |
|---|---|
| Supabase organisation `Procsense` (Free plan) | Created. Owner: Moiz. Invitations sent to Ruhan (to be administrator) and Sannan; they will accept later. |
| Supabase project `procsense-dev` | Created. Reference `qgnirvxfknyacwovkugt`, region Southeast Asia (Singapore, `ap-southeast-1`), PostgreSQL 17.11. |
| Supabase project `procsense-prod` | **Not created.** A person may have at most 2 active free projects across all organisations they administer, and Moiz already has 2 (this one and an older one). Created when the team moves to a paid plan (DT-123, DT-133). Until then `procsense-dev` is the only project and **may hold real trainee data** (owner's decision, 13 October 2026). Because it is then the only copy on a plan with no automatic backups, the conditions in "Before real trainees use it" apply. |
| Container host (Render, free web service) | Account exists (Moiz's, from an earlier web application). The 750 free hours a month are shared by every free service in a workspace, so ProcSense gets its own workspace (a Render team named `Procsense`) rather than sharing the old project's hours. The service is created at the end of `p1-backend-s01`, when the Dockerfile exists. The way Render receives the code (image from GitHub's container registry, or Render's GitHub app) is decided with Ruhan: the plan prefers CI-built images (the tested image is the one that runs, and no Render build minutes are used). |
| Email for sign-in messages | Supabase's built-in email is for testing only (2 messages an hour, team members only), so invitations and sign-up need a custom sender. Today: Gmail SMTP with an app password (about 500 messages a day, rate limit in Supabase 30 an hour). Development only: it can land in spam. Before real customers, a sender on our own domain (DT-132). |

## Before real trainees use it
The one project holds real trainee data, so before the first real trainee signs in:
1. The nightly backup and a tested restore exist (`p1-backend-s13`), because the free plan keeps no backups.
2. Consent and the data map exist (`p1-backend-s12`), because accounts and results are personal data.
3. Public sign-ups are off and accounts are created by invitation.
4. Test sessions use a separate test organisation (named `ProcSense Test`), so test data never mixes with a real organisation's totals.
5. The data API is off and row-level security is on for every table (`p1-backend-s08`).

## Settings that must stay as they are
- **Data API off** for the project. The FastAPI service is the only client of the database.
- **Row-level security** on every table, with no policies for the public roles (added with the tables in s08).
- **Confirm email** on. Public sign-ups are on only while nobody real uses the project; from the first real trainee they are **off** and accounts are created by invitation.
- The application connects through the **transaction pooler** (port 6543, prepared statements off). Migrations, backups and restores use the **session pooler** (port 5432). The plain direct address is IPv6-only on the free plan and is not used.

## Where secrets live
| Secret | Where | Never |
|---|---|---|
| Database password, both pooler addresses | Password manager; host environment variables; GitHub Actions secrets; a git-ignored `server/.env` | git, chat, WhatsApp, screenshots |
| Supabase secret key (`sb_secret_...`) | The same places | The headset, a web page, git |
| Supabase publishable key (`sb_publishable_...`), project URL | Not secret. Used by the headset and web pages. | |
| Gmail app password for the sender | Password manager and Supabase's SMTP form | git |
| Render deploy hook, registry access token | Password manager; GitHub Actions secrets | git |

`server/.env.example` lists every variable with a fake value. CI fails if a Supabase secret key, a database address with a real password, a signed token or a private key is committed.

## Reaching things
- **Database:** Supabase dashboard > SQL Editor for quick checks. From a laptop: copy the session-pooler address from Connect > Direct; the user is `postgres.<reference>`. Percent-encode special characters in the password.
- **Logs:** Supabase dashboard > Logs. Render (once it exists): the service's Logs tab.

## When something stops
| Symptom | Cause | Do this |
|---|---|---|
| The dashboard says the project is paused | Free projects pause after 1 week of inactivity | Open the project in the dashboard and choose Restore. Wait a few minutes. Then find out why the keep-alive stopped: the Scheduled jobs workflow failed, or GitHub disabled the scheduled workflows after 60 days without a commit (see the exit list). |
| The first request to the service takes about a minute | Render's free service sleeps after 15 minutes idle and wakes in about a minute | Wait and retry. The headset's upload queue already retries. Wake it before a demonstration by opening `/health`. |
| An invitation email does not arrive | Wrong sender settings, Gmail refused the login, or the message went to spam | Check the spam folder, then Supabase > Logs; recreate the Gmail app password if needed. |
| Prepared statement errors from the database | The app is connected through the transaction pooler with prepared statements on | Switch them off for that connection (s01). |
| Everything under `procsense-dev` is lost | The free plan has no automatic backups | The nightly dump of the application tables (`p1-backend-s13`) is the only copy. Accounts are not in it (DT-130): people are re-invited. This is why the backup must exist before real trainees use the project. |

## Making the first organisation and the first platform administrator
There is no screen for this (DT-129). From a laptop with `server/.env` filled in:
1. Sign in once with the email address that should become the platform administrator (the profile is created on first sign-in), and copy the person's id from Supabase's Authentication > Users page.
2. `task server-admin -- grant-platform-admin --user-id <that id>`
3. `task server-admin -- create-organisation --name "Alpha Hospital" --slug alpha-hospital --admin-email boss@example.com --send` creates the organisation and invites its first administrator (`--send` also asks Supabase to email the invitation; without it the invitation still applies when that address signs in).
Invitations hold the invited address only until they are accepted, revoked or expired.

## Which content versions the service can verify
`tools/content/package_versions.py` puts three folders in the image: the practice package, the assessment package, and the whole `content/` folder named by the first 12 characters of its hash (what a development build or the Unity Editor writes as its version). The last is left out with `--no-dev` for a release image (DT-162). A case that is not approved for the session's mode is refused by the engine in every version. A new content commit means a new image: a session with a version the image does not hold is stored as `unverifiable`.

## Verification of uploaded sessions
Every uploaded session is replayed by the engine before it counts (p1-backend-s09). The image holds the engine (`/opt/engine/bin/procsense-sim`, a Linux program) and the content of each version it can verify (`/opt/engine/content/<version>/`, one for practice and one for assessment). `task server-engine-prepare` builds both into `server/engine/`, and CI does it before the image is built.
- A session waits as `pending` until verified. It is judged in the background right after the upload, by a sweep when the service wakes, and by `POST /internal/verify-pending` with the header `X-Internal-Key` (the secret `INTERNAL_KEY`; a scheduled job calls it, p1-backend-s13).
- Outcomes: `verified` (counts), `rejected` (the log is not what its header says, or the case is not allowed for its mode), `unverifiable` (this service cannot judge it: another engine or content version, a generated scenario; kept, never counted). After 3 attempts without a verdict from the engine a session becomes `unverifiable` with `VERIFIER_ERROR`.
- **When a new headset release uses new content or a new engine,** its sessions are `unverifiable` until the service has that content version (rebuild the image) and, if `SUPPORTED_ENGINE_VERSIONS` is set, that engine version listed.
- The engine runs with a time limit, a 64 MiB file-size limit and without any secret in its environment. A session that stays `pending` is explained by the host's log line `verification of <id> gave no verdict (...)`: `exit 134` or `exit 127` mean the engine could not start (check the image), `killed by signal SIGXFSZ` means a limit was hit (the .NET runtime's memory layout needs `DOTNET_EnableWriteXorExecute=0`, which the image sets), `timeout` means the host's CPU was too slow (raise `VERIFY_TIMEOUT_SECONDS`).

## Putting the service on the host (Render) and keeping it alive
The image is built on GitHub (`.github/workflows/publish-image.yml`: the Linux engine, the content of each version, the service) and pushed to `ghcr.io/<owner>/procsense-server`; Render pulls it. Nothing secret is in the image. Steps, once:
1. **Tables:** with `server/.env` filled in (session pooler address in `MIGRATION_DATABASE_URL`), `task server-migrate`.
2. **Image:** push a `myt1x54/backend-*` branch (or main); the Actions tab shows the run; the package appears under the repository's Packages. It is private. Make a classic personal access token with only `read:packages` (30 days) for Render to pull it.
3. **Render:** workspace `Procsense` > New > Web Service > Existing Image > `ghcr.io/<owner>/procsense-server:latest`, the token as the registry credential, region Singapore, instance type Free, health check path `/health`. Environment variables (typed into Render, never into chat or git): `APP_ENV=prod`, `SUPABASE_URL`, `SUPABASE_PUBLISHABLE_KEY`, `SUPABASE_SECRET_KEY`, `DATABASE_URL` (the **transaction** pooler, port 6543), `INTERNAL_KEY` (a long random string), `DEVICE_VERIFICATION_URI=https://<the service>/device`. The engine settings are already in the image.
4. **Check:** `/health` shows the commit the image was built from; `/health/db` says the database answers.
5. **Supabase** > Authentication > URL Configuration: Site URL is the service's address; add `<address>/` and `<address>/reset` as redirect addresses (DT-150). Public sign-ups off.
6. **First people:** sign in on the web page once, `task server-admin -- grant-platform-admin --user-id <id>`, `task server-admin -- create-organisation ...`, then add the others (People page, or `add-member`). Each person ticks the first consent box once.
7. **Scheduled jobs:** GitHub > Settings > Secrets and variables > Actions: `SERVICE_URL` and `INTERNAL_KEY` (the same value as on Render); optional `RENDER_DEPLOY_HOOK` makes every new image deploy by itself. `.github/workflows/scheduled-jobs.yml` then wakes the service, touches the database and runs the verification sweep every 3 hours and the retention clean-up daily, and fails (and emails) when the service does not answer.
8. **Prove it:** `task demo-session`, then `task headset-sim -- --url <address> --file demo_session.jsonl`; approve the code on the web page; the status should become `verified`. `server/DEMO.md` is the checklist for a demonstration.
The free-plan exit list is in Free-plan limits and the exit list (below). The 1,000-session load test passed on 16 Oct 2026 (EXP-BE-3 in docs/plans/p1/p1-backend/README.md; rerun with `task server-load-test -- --database-url <a local database>`). The nightly backup and the size monitor exist: see Backup and restore.

## Backup and restore
The free plan keeps no backups, so the nightly job is the only copy of the data.
- **What runs:** `.github/workflows/backup.yml`, every night at about 02:23 UTC (and by hand: Actions > Nightly backup > Run workflow). It takes one consistent snapshot of the application tables (schema `public`; Supabase's accounts are not in it, DT-130), encrypts it (AES-256-GCM, key from `BACKUP_KEY`), restores it into a throwaway PostgreSQL and checks that every table has the same number of rows, and keeps it as the private artifact `procsense-backup` for 14 days. It fails, and opens an issue (GitHub emails the repository's people), when the dump is empty, less than half the size of the last good one, cannot be restored, or the secrets are missing.
- **Set up once:** make a key and store it in a password manager (without it the backups are unreadable, and the repository secret cannot be read back):
  `python -c "import secrets; print(secrets.token_urlsafe(32))"`
  Then GitHub > Settings > Secrets and variables > Actions > New repository secret `BACKUP_KEY`. The database address is the existing `MIGRATION_DATABASE_URL` (session pooler).
- **Restoring from the last night:** Actions > Nightly backup > the latest green run > download the artifact `procsense-backup` and unzip it. Put the key in the environment (`BACKUP_KEY`), start from an **empty** database (a new Supabase project, or `docker run -d -p 55432:5432 -e POSTGRES_PASSWORD=x postgres:17`), then:
  `uv run --project server python -m server.scripts.backup restore --file <the .dump.enc file> --url <address of that database>`
  The restore replaces the tables of the schema `public` (`--clean`), so point it at the right database. Then point the service's `DATABASE_URL` at it. People sign in again with their Supabase accounts: those are not in the backup, so for a new Supabase project they are invited again (DT-130).
- **Proving it without a disaster:** `uv run --project server python -m server.scripts.backup drill --file <the .dump.enc file>` (needs Docker). The nightly job does this every night; the time it took is in the run summary.
- **The size monitor:** `.github/workflows/monitor.yml` runs daily and fails, with an issue, when the database passes 80% of the free 500 MB. Locally: `uv run --project server python -m server.scripts.backup monitor --url <session pooler address>`. Whether the service itself answers is checked by the Scheduled jobs workflow.
- **What it does not cover:** changes made after the last night's dump (at most a day of sessions; a trainee's headset keeps the unsent ones in its outbox and sends them again), and Supabase's accounts. Moving to a paid plan adds Supabase's own daily backups on top of this.

## Deploying and going back (continuous delivery)
Every commit on `main` whose CI is green is deployed by `.github/workflows/deploy.yml`: image built and pushed (`latest` and the commit id), migrations applied, the host told to deploy, `/health` and `/health/db` checked until the service runs that commit. Set these repository secrets once (GitHub > Settings > Secrets and variables > Actions; typed there, never in chat): `SERVICE_URL` (the service address), `RENDER_DEPLOY_HOOK` (Render > the service > Settings > Deploy Hook), `MIGRATION_DATABASE_URL` (the **session** pooler address, port 5432), and `INTERNAL_KEY` for the scheduled jobs. A failed deploy opens an issue; the host only switches to the new version when it is healthy, so the old one keeps running.
- **Migrations must be additive** (add a column or table; never rename or drop in the same release that stops using the old shape), because the old version runs against the new schema for a minute, and so a deploy can be undone without undoing the database. Every migration still has a `downgrade`.
- **Going back to an earlier version:** every commit that was deployed stays in the registry as `ghcr.io/<owner>/procsense-server:<full 40-character commit id>` (`git rev-parse <short id>` gives it). In Render open the service > Settings > Build & Deploy > Image URL, replace `latest` with that tag, and Save (Render deploys it). Check `/health`: its `git_sha` is the commit you chose. To come back to the newest version put `latest` back and save. A migration that was applied stays; additive ones do not hurt the old version.
- **While the service is pinned to an old tag,** the next push to main redeploys the pinned old image and the Deploy workflow reports that the service does not run the new commit (and opens an issue). So after going back, either fix forward quickly (a new commit on main that undoes the bad change, then put `latest` back), or pause deploys (below) until you are ready.
- **Rehearsal (14 October 2026, DT-166):** the steps above were tried once from `latest` (`93ef6fb`) back to `88155a39eabb1fcaa47d6c19cf4ace9ccad00558` and forward again; it took about 30 seconds in each direction (Render's own deploy list showed 30.3 s back to the old image and 30.4 s forward to `latest`), `/health` showed the chosen commit each time, and no data was touched.
- **Pausing deploys:** disable the Deploy workflow in GitHub > Actions, or remove the `RENDER_DEPLOY_HOOK` secret: the image is still built and nothing is deployed.
- **Deploying by hand:** Actions > Deploy > Run workflow (builds the commit of the branch you choose, moves `latest` and deploys it; use main).

## Privacy and retention
`server/PRIVACY.md` is the data map (what is held, where, who reads it, how long). Operating it:
- **Daily clean-up:** `python -m server.jobs.retention` or `POST /internal/retention` with the internal key (p1-backend-s13 schedules it). It removes the stored recording and report page of sessions older than the organisation's period (24 months unless an administrator chose 6 to 60 on the Privacy page), rejects pending sessions that were purged, deletes access-log rows older than 24 months, removes expired headset sign-in requests (a day after they expired) and headset sign-ins revoked or expired a month ago, and clears the email of invitations nobody accepted a month after they expired. Results and totals are never touched.
- **A person asks to be deleted:** they do it themselves on the Privacy page. If they cannot sign in, run `uv run --project server python -m server.scripts.admin delete-person --user-id <their id from Supabase's Authentication page> --confirm DELETE`: it does what the Privacy page does (cuts the links to their sessions, removes their consents, history and headset sign-ins, then removes the sign-in account; the sessions stay, so totals do not change). If the sign-in service refuses, nothing is changed and the command can be run again. It refuses for the last administrator of an organisation.
- **A new consent text:** change `CONSENT_VERSION` in `server/privacy.py`; everyone is asked again at their next sign-in and uploads from people who have not agreed to the new text are refused (`403 consent_required`) until they do.
- Never put an email, name or token in a log line on purpose; the cleaner (`server/logging_setup.py`) is a safety net, not permission.

## The web pages
The service also serves the web pages (`server/static/`, one small page with plain JavaScript, no build step): `/` (sign in and the dashboard), `/device` (approve a headset code; the code in the address `?code=` is filled in), `/reset` (password reset). `/config.json` gives the page the Supabase project address and the publishable key.
- **Try them on a laptop** with sample data and a pretend sign-in (never in production): `TEST_DATABASE_URL=... uv run --project server python -m server.tests.browser_stack --port 8765`, then open http://127.0.0.1:8765/ and sign in as `admin@alpha.test`, `supervisor@alpha.test`, `trainee@alpha.test` or `supervisor@beta.test` with the password the command prints when it starts.
- The access token stays in the page's memory: a reload signs the person out (DT-149).
- **Before the first real use,** in the Supabase dashboard set the Site URL and the redirect addresses (Authentication, URL configuration) to the service's address, so invitation and password-reset emails open `/reset` and the sign-in page here and not the default address (DT-150).
- **By hand, once per release** (the browser test cannot judge these): on a phone-sized window every page is readable and nothing scrolls sideways except the tables; every page works with the keyboard only (Tab, Enter, Space) and the focus is always visible; a screen reader (NVDA or VoiceOver) announces the headings, the form labels and the messages; text and button colours are readable at 200% zoom; the report frame is readable.

## Reports, dashboard and export
`GET /organisations/{id}/sessions` (a list with filters), `/sessions/{id}/report` and `/report.html`, `/progress` (the signed-in person), `/members/{user}/progress`, `/overview`, `/cohorts/{id}/overview` and `/export.csv` serve a web dashboard (p1-backend-s11). Only **verified** sessions are counted. A trainee sees their own sessions; a supervisor or administrator sees the whole organisation.
- The columns of the CSV are in `server/DATA_DICTIONARY.md`. The platform administrator's `GET /export.csv` covers every organisation with a stable code in place of the name.
- A supervisor opening another person's report, progress or an export is written to the log named `procsense.access` (a table with the privacy work, p1-backend-s12).
- Every process score is marked provisional while the scorer's weights are (they are today).

## Headsets signed in with a code
A headset asks `POST /auth/device/start`, shows the code, and a person signed in on the web approves it (`POST /auth/device/decision`, which the dashboard's `/device` page calls). The headset then receives its own access token (15 minutes) and refresh token (30 days of not being used). Only the SHA-256 of each is in the database.
- **Sign a headset out:** the person does it from the web (`DELETE /me/devices/{id}`), or the headset signs itself out (`POST /auth/device/logout`). It is refused on its next request. Deactivating a member in an organisation also stops the headset reaching that organisation.
- **A refresh token used twice** (a copy was made) revokes that headset's sign-in; the headset has to sign in with a code again. The security log line is `procsense.security`; the database row says `refresh_token_reused`.
- A headset's token works for training (sessions, `/me`). Administering an organisation, approving other headsets and changing the account need a web sign-in.
- Expired login requests are not deleted yet (DT-142).

## Rotating a key
1. **Supabase secret key:** Project Settings > API Keys > New secret key; put the new one everywhere in the table above; check the service works; delete the old one.
2. **Database password:** Project Settings > Database > reset the password; update both pooler addresses everywhere; redeploy.
3. **Gmail app password:** revoke it in the Google account and create a new one; update Supabase's SMTP form.
4. **After any leak** (a key in git, chat or a screenshot): rotate first, then clean up. Deleting from git history does not make a leaked key safe.

## Free-plan limits and the exit list (checked 18 October 2026)
ProcSense is a final-year project and runs on free plans only (decision of 18 Oct 2026). For each limit: what we use now, what watches it, when to act and what to do. Paying is the last step, not the first; it is the owner's decision.

| Service and limit | Our use now | Watched by | Act when | What to do |
|---|---|---|---|---|
| **Supabase database: 500 MB** | 1,000 sessions take about 20.8 MB (about 12 KB each with their score rows, EXP-BE-3), so about 32,000 sessions fit | The Database monitor workflow (daily; opens an issue at 80%) | The monitor's issue (400 MB) | First shorten the retention period (an organisation can choose 6 months; the stored log and report page go, the results stay). If that is not enough: Supabase Pro (from USD 25 a month: 8 GB, daily backups kept 7 days, no pausing), or move to another PostgreSQL (the nightly backup restores into any PostgreSQL 17: the drill in Backup and restore) |
| **Supabase pauses a project after 7 days of inactivity** | The Scheduled jobs workflow calls `/health/db` (a database query), the verification sweep and, daily, the retention clean-up: at least 8 database calls a day (checked 18 Oct 2026: runs #23 and #24 green, `/health/db` 8 ms) | The Scheduled jobs workflow fails and emails when the service or the database does not answer | It fails two days running, or the project shows as paused | Restore the project in the dashboard (When something stops), then fix the scheduled workflow |
| **GitHub disables scheduled workflows in a public repository after 60 days without activity** | The keep-alive, the nightly backup and the monitor are all scheduled workflows | Nobody: GitHub does it silently | Before a break of more than about 6 weeks with no commit (for example after the final evaluation) | Push any small commit before the break; after one, open Actions and choose **Enable workflow** for Scheduled jobs, Nightly backup and Database monitor, then run the backup by hand |
| **Supabase egress: 5 GB a month** | Small: uploads, dashboards and exports of a few MB | Supabase's Usage page | 80% | Export less often; Supabase Pro includes 250 GB |
| **Supabase: no automatic backups** | The Nightly backup workflow: an encrypted dump, its restore proved each night, kept 14 days | The workflow opens an issue when a backup fails or shrinks by half | Any failure | Run the backup by hand (Actions, Nightly backup, Run workflow) the same day |
| **Supabase built-in email: 2 an hour** | Custom SMTP (Gmail app password), 30 an hour | Invitations not arriving | Many invitations at once | Send them over several hours, or another SMTP provider |
| **Render free web service: sleeps after 15 minutes idle; 750 instance hours a month per workspace; outbound bandwidth and build minutes have monthly included amounts, and without a payment method the service is suspended or builds stop when they run out; may restart at any time** | One service; it wakes in about 33 s (measured 16 Oct 2026); sleeping hours do not count | The Deploy workflow and `/health` | Before a demonstration (to avoid the wake-up), or a suspension notice from Render | Open `/health` two minutes before a demonstration. Do **not** add a ping to keep it awake: that spends the 750 hours. If Render suspends it: run the same image on a team laptop with Docker for the demonstration, or the cheapest paid Render instance (owner's decision; the price was not read today) |
| **GitHub Actions** | Free and unlimited on standard runners while the repository is public (since 18 Oct 2026). A private repository gets 2,000 minutes a month on GitHub Free, which ProcSense used up in about five days | The workflow runs | The repository becomes private again | Run CI only on main and pull requests (the local `task ci` and the pre-push hook check every branch), or a self-hosted runner on a team machine |
| **Git LFS: 10 GiB storage and 10 GiB download bandwidth a month, counted on the owner's account (also for a public repository); with a USD 0 budget it is blocked for the rest of the month when used up** | 13 files, 44 MB (lighting and the patient's idle animation). CI does not download LFS files | Nobody automatically | Storage over about 5 GiB, or many fresh clones in a month | Keep only what the build needs in LFS; never switch on `lfs: true` in a CI checkout |

Sources read on 18 Oct 2026: supabase.com/pricing, render.com/docs/free, docs.github.com (Actions billing, Git LFS billing, disabling and enabling workflows).

## Open items
- Ruhan and Sannan accept the Supabase invitation; Ruhan becomes administrator and a second owner of every account.
- A Render workspace for ProcSense and the service (end of s01); the way Render receives the code is decided with Ruhan (CI-built image preferred).
- Done: the calendar reminder to open `procsense-dev` every few days until the keep-alive existed (Moiz); the keep-alive runs since 15 Oct 2026, so the reminder is no longer needed.

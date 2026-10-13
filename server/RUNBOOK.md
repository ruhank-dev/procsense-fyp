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
| The dashboard says the project is paused | Free projects pause after 1 week of inactivity | Open the project in the dashboard and choose Restore. Wait a few minutes. Until the keep-alive exists (`p1-backend-s13`), open the project every few days. |
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

## Verification of uploaded sessions
Every uploaded session is replayed by the engine before it counts (p1-backend-s09). The image holds the engine (`/opt/engine/bin/procsense-sim`, a Linux program) and the content of each version it can verify (`/opt/engine/content/<version>/`, one for practice and one for assessment). `task server-engine-prepare` builds both into `server/engine/`, and CI does it before the image is built.
- A session waits as `pending` until verified. It is judged in the background right after the upload, by a sweep when the service wakes, and by `POST /internal/verify-pending` with the header `X-Internal-Key` (the secret `INTERNAL_KEY`; a scheduled job calls it, p1-backend-s13).
- Outcomes: `verified` (counts), `rejected` (the log is not what its header says, or the case is not allowed for its mode), `unverifiable` (this service cannot judge it: another engine or content version, a generated scenario; kept, never counted). After 3 attempts without a verdict from the engine a session becomes `unverifiable` with `VERIFIER_ERROR`.
- **When a new headset release uses new content or a new engine,** its sessions are `unverifiable` until the service has that content version (rebuild the image) and, if `SUPPORTED_ENGINE_VERSIONS` is set, that engine version listed.
- The engine runs with a time limit and without any secret in its environment.

## Rotating a key
1. **Supabase secret key:** Project Settings > API Keys > New secret key; put the new one everywhere in the table above; check the service works; delete the old one.
2. **Database password:** Project Settings > Database > reset the password; update both pooler addresses everywhere; redeploy.
3. **Gmail app password:** revoke it in the Google account and create a new one; update Supabase's SMTP form.
4. **After any leak** (a key in git, chat or a screenshot): rotate first, then clean up. Deleting from git history does not make a leaked key safe.

## Free-plan limits (checked 13 October 2026)
| Service | Limit | Source |
|---|---|---|
| Supabase Free | 2 active projects per person, 500 MB database and 500 MB memory per project, 50,000 monthly active users, 5 GB egress, pauses after 1 week of inactivity, no automatic backups | supabase.com/pricing |
| Supabase built-in email | 2 messages an hour, only to team members, no delivery guarantee; invitations and password sign-up need custom SMTP (30 an hour once set) | supabase.com/docs/guides/auth/auth-smtp |
| Render free web service | Sleeps after 15 minutes idle (about 1 minute to wake), 750 free hours a month, no persistent disk, may restart at any time, not for production; free Postgres expires after 30 days (not used) | render.com/docs/free |

## Open items
- Ruhan and Sannan accept the Supabase invitation; Ruhan becomes administrator and a second owner of every account.
- A Render workspace for ProcSense and the service (end of s01); the way Render receives the code is decided with Ruhan (CI-built image preferred).
- Done: the calendar reminder to open `procsense-dev` every few days until the keep-alive exists (Moiz).

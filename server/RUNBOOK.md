# Runbook: hosting the ProcSense backend (p1-backend-s07)

Written 13 October 2026. Free plans change their limits, so recheck the numbers below before relying on them and update the date. No secret appears in this file; secrets are in the team password manager (see "Where secrets live").

## What is running today
| Part | State |
|---|---|
| Supabase organisation `Procsense` (Free plan) | Created. Owner: Moiz. Invitations sent to Ruhan (to be administrator) and Sannan; not yet accepted. |
| Supabase project `procsense-dev` | Created. Reference `qgnirvxfknyacwovkugt`, region Southeast Asia (Singapore, `ap-southeast-1`), PostgreSQL 17.11. |
| Supabase project `procsense-prod` | **Not created.** A person may have at most 2 active free projects across all organisations they administer, and Moiz already has 2 (this one and an older one). Created when the team moves to a paid plan (DT-123, DT-133). Until then `procsense-dev` is the only project and **holds no real trainee data**. |
| Container host (Render, free web service) | Account to be created; the service is created at the end of `p1-backend-s01`, when the Dockerfile exists. The way Render receives the code (image from GitHub's container registry, or Render's GitHub app) is decided with Ruhan: the plan prefers CI-built images (the tested image is the one that runs, and no Render build minutes are used). |
| Email for sign-in messages | Supabase's built-in email is for testing only (2 messages an hour, team members only), so invitations and sign-up need a custom sender. Today: Gmail SMTP with an app password (about 500 messages a day, rate limit in Supabase 30 an hour). Development only: it can land in spam. Before real customers, a sender on our own domain (DT-132). |

## Settings that must stay as they are
- **Data API off** for the project. The FastAPI service is the only client of the database.
- **Row-level security** on every table, with no policies for the public roles (added with the tables in s08).
- **Confirm email** on. Public sign-ups are on in `procsense-dev` for testing only; accounts are created by invitation in production.
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
| Everything under `procsense-dev` is lost | The free plan has no automatic backups | The nightly dump of the application tables (`p1-backend-s13`) is the only copy. Accounts are not in it (DT-130): people are re-invited. Until s13 exists there is nothing to lose that cannot be recreated, because the database holds only test data. |

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
- Render account and the service (end of s01); the way Render receives the code (with Ruhan).
- A calendar reminder to open `procsense-dev` every few days until the keep-alive exists.

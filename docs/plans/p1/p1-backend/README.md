# p1-backend

**Tier:** P1 (built early, see below) · **Builds on:** p0-core-engine, p0-scoring-debrief, p0-case-library · **Feeds:** p1-ai-language, p1-voice-input, p0-vr-interaction-ui (login and debrief), evaluation (reports and exports) · **Decision:** [ADR-0009](../../../decisions/ADR-0009-supabase-postgres-cloud-backend.md)

## Goal
A hosted service for the product that will be sold: people sign in (in the VR application and on the web), the headset uploads finished sessions, the server checks that each score is genuine, and supervisors see results across trainees, cohorts and organisations on a global dashboard. It also hosts the AI gateway and the AI audit log. **The headset still plays fully offline**: the engine runs on the device, and the network is only needed to sign in, to upload and to read history.

## Work order
This phase moves ahead of the rest of P1. Its accounts, upload, verification, dashboard and operations steps form milestone **M17** and are built alongside the VR work, in this order: `s07` hosting and environments, `s01` service skeleton, `s08` organisations and accounts, `s02` session upload, `s09` verification, `s10` Quest login, `s03` Quest upload client, `s04` reports and dashboard data, `s11` web dashboard, `s12` privacy, `s13` operations. The audit log (`s05`) and the AI gateway (`s06`) stay in M10 with the AI work; AI hosting for a sold product is a separate decision (OQ-07, DT-122).

## Scope
- **In:** FastAPI (routers / repositories / schemas, INSTRUCTIONS §4b), PostgreSQL on Supabase through SQLAlchemy 2 (ORM only) and Alembic, Supabase Auth for accounts, organisations with roles and cohorts, session upload with an offline queue on the headset, server-side verification by engine replay, JSON reports, a dashboard and CSV export for supervisors, privacy handling (consent, retention, delete my data), free-tier operations (backups, keep-alive), the audit log, the AI gateway, two-stage Docker, uv.
- **Out:** payments and billing (DT-125); model logic (p1-ai-language); single sign-on (DT-124); a platform-admin screen (DT-129); where the AI models run for customers (DT-122).

## Architecture
```
Quest application (engine on the device, works offline)
   |  sign in, then upload finished sessions over HTTPS (queued, retried)
   v
FastAPI service (one container, free host)  ---- serves the web dashboard (static pages + JSON API)
   |  checks the login token, the organisation and the role on every call
   |  replays each uploaded session through the engine before it counts
   v
Supabase: PostgreSQL (our tables) + Auth (accounts)      only the service talks to the database
```

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Database | PostgreSQL on Supabase (free plan first) | SQLite (the old plan); Firebase; Neon + own login | Relational data and SQL reporting across organisations; plain Postgres can move to any host; the ORM-only rule keeps the code portable | The free plan stops being enough, a customer needs a particular country or their own premises |
| Accounts | Supabase Auth; roles and organisations in our own tables | Own login in FastAPI; per-device keys | Login, password reset and email are solved problems; roles stay in our database so they can be tested and audited | Single sign-on per organisation is needed (DT-124) |
| Who reaches the database | Only the FastAPI service; row-level security switched on with no open policies as a second lock | The headset or web page using Supabase's data API directly | Every rule (role, organisation, verification) lives in one tested place | — |
| Tenancy | Organisation, membership with a role, cohort, from the first table | Single tenant first | Adding it later rewrites every table and query | — |
| Headset login | Email and password first; then a device code approved on a phone or computer | Password only | Typing in VR is painful | Quest users find the code flow worse (EXP-BE-4) |
| Score integrity | The server replays each log through the engine and refuses a score that does not reproduce | Trust the headset | A modified headset must not be able to send a better score | — |
| Sync | The headset keeps its session log and uploads when reachable | Always online | Offline-first | — |
| Dashboard | Static pages and JSON served by the same FastAPI app; Chart.js for charts | React single-page application; Supabase UI | One free deployment, small scope | Supervisors want interactive analysis (DT-128) |
| Hosting (free, student project) | Supabase free plan + Render free web service; replaceable | Neon, Oracle Always Free VM, Google Cloud Run, Azure for Students | Free, no server to administer, Docker keeps it portable | Before real customers (DT-123) |
| Supabase connections | Application: Supabase's connection pooler (works over IPv4); migrations: session-mode connection; prepared statements off through the pooler | The direct connection (IPv6 only on the free plan) | Avoids the free plan's connection limits and the IPv6 problem | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-BE-1 | Upload reliability from the headset | 20 uploads with the network dropped mid-way, on university Wi-Fi and on a phone hotspot | 0 lost sessions, 0 duplicates |
| EXP-BE-2 | LLM + ASR on one 6 GB GPU? | Concurrent load test (in `s06`) | Both meet latency, else split across 2 machines |
| EXP-BE-3 | Does the free tier hold a realistic load? | 1,000 synthetic sessions across 5 organisations into the free plan; dashboard queries; a cold start of the free container; a pause-and-restore drill of the database | Database under 400 MB; every dashboard query under 1 s; wake-up under 60 s; restore from our backup works |
| EXP-BE-4 | Can a person sign in from the headset? | 5 people sign in with the form and with the device code | Median time under 60 s and no one needs help |
| EXP-BE-5 | Does verification catch a modified result? | Change one score, one action and the content version in a log before upload | All three refused with a reason |

## Exit
Every endpoint has schemas, tests and `/docs`; a person signs in on the headset and on the web; a finished session uploads, is verified and appears on the right supervisor's dashboard and nowhere else (another organisation's supervisor cannot see it); the game is unaffected with the service off; backups and the keep-alive run; the data-protection steps (`s12`) are done.

## Who does what
Moiz leads the service, the database and the dashboard (owner of B-M16). Ruhan: the verification command in the engine and the AI gateway (`s06`). Sannan: the Quest login and upload client (`s03`, `s10`). Together: the Supabase and hosting accounts (`s07`), who owns the credentials, and the privacy decisions (`s12`).

## Sources
Master Plan §25; Implementation Guide B-M16, B-M10; ADR-0009.

# p1-backend

**Tier:** P1 · **Builds on:** p0-core-engine · **Feeds:** p1-ai-language, p1-voice-input, evaluation (reports)

## Goal
An optional local server (laptop/PC on the same Wi-Fi as the headset) that stores session results, produces reports for trainees
and the supervisor, hosts the AI models (LLM, speech recognition), and keeps the AI audit log. **The headset works fully without it.**

## Scope
- **In:** FastAPI (routers / repositories / schemas — INSTRUCTIONS §4b), SQLite via SQLAlchemy ORM + Alembic, Pydantic settings,
  session upload with offline queue on the headset, HTML/JSON reports, AI gateway endpoints, audit logger, LAN auth, two-stage Docker, uv.
- **Out:** model logic (p1-ai-language).

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Framework | FastAPI + Pydantic | ASP.NET Core; Flask | Rules + Python AI ecosystem | — |
| Database | SQLite + SQLAlchemy + Alembic | PostgreSQL | Single machine, zero ops | Concurrent multi-user writes → Postgres |
| Sync | Headset keeps JSONL, uploads when reachable | Always-online | Offline-first | — |
| Auth | Per-device API key from `.env`, LAN only | User accounts | Simple, secure enough | Multi-site use |
| Reports | Server-rendered HTML | React dashboard | Small scope | Supervisor wants interactive dashboard |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-BE-1 | Upload reliability on university Wi-Fi | 20 uploads, drop network mid-way | 0 lost sessions |
| EXP-BE-2 | LLM + ASR on one 6 GB GPU? | Concurrent load test | Both meet latency, else split across 2 machines |

## Exit
Every endpoint has schemas + tests + `/docs`; full session uploads and renders a report; game unaffected with server off.

## Who does what
Developer: all. Team: network setup where it's used.

## Sources
Master Plan §25; Implementation Guide B-M16, B-M10.

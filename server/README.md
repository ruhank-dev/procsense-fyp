# server/

Hosted backend (P1, milestone M17, ADR-0009): FastAPI + PostgreSQL on Supabase for accounts and organisations, session upload with server-side verification, the web dashboard, reports and the AI endpoints.
Follows INSTRUCTIONS §4b (routers / repositories / schemas), ORM only, settings via Pydantic, `uv`.
Built in the steps of `docs/plans/p1/p1-backend/`. `server/validation/` (the grounding validator) is already here.

# Demonstration checklist (p1-backend-s13)

What to show: a person signs in, a session is played, it reaches the database, the engine replays and verifies it, and the result appears for the person and for their supervisor. No real patient is involved; the people are made up or the team.

## The day before
1. Open `https://<the service>/health` and `/health/db`. Both should answer within a minute (the free host sleeps after 15 minutes idle; the first call wakes it).
2. Supabase dashboard: the project `procsense-dev` is not paused (a free project pauses after a week without activity; the scheduled job in `.github/workflows/scheduled-jobs.yml` touches it every 3 hours once the repository secrets are set).
3. GitHub > Actions: the last "Publish the service image" run is green and its commit is the one you want to show. The headset build must come from the same commit (the content version has to match, or sessions are stored but not verified).
4. Make the demo people: each signs in on the web page once and ticks the first consent box. One is a trainee, one a supervisor of the same organisation (People page, or `task server-admin -- add-member --slug <organisation> --user-id <id> --role supervisor`).
5. Make a session file: `task demo-session -- --out demo_session.jsonl` (default case `VAR-S1-E`, which ends with a verdict). Try the whole chain once without the headset: `task headset-sim -- --url https://<the service> --file demo_session.jsonl`, approve the code on the web page, and look for `"status": "verified"`.

## Ten minutes before
6. Wake the service again (`/health`) and keep one browser tab on the web page signed in as the supervisor, on the Overview page.
7. Use a phone hotspot if the venue's Wi-Fi is unknown (some block new sites or make the first request fail).

## The story, in order
1. Sign in (the headset or the web page): "accounts and organisations, roles checked on every request".
2. Play or replay the session. The session file is saved on the device first, so nothing is lost without a network.
3. It uploads: "stored once, even if sent twice".
4. Wait a few seconds: the status changes from pending to verified. "The server replays every action with the same engine and the same content; a changed file is refused."
5. The supervisor's Overview and the person's Progress change. Open the report page.
6. Privacy page: who looked at my data, download my data, delete my data (do not press delete in the demonstration).

## If something fails
- The service does not answer: wait a minute and retry, then use the fallback below.
- A session stays `pending`: the engine inside the service did not give a verdict; `POST /internal/verify-pending` retries it (needs the internal key), and the log in the host's dashboard says why.
- A session is `unverifiable`: the headset's content version is not one the service holds. Rebuild the image from the same commit as the headset build.
- A session is refused with `consent_required`: that person has not ticked the consent box on the web page.
- Fallback: run the service on the laptop (`uvicorn server.main:app` with the `server/.env` settings) behind an HTTPS tunnel, or show the recorded run of the same chain.

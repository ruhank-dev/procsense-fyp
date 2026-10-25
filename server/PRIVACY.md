# Privacy: what ProcSense keeps about people (p1-backend-s12)

This is the data map of the service in `server/`. It describes what the software really does today. **It is not legal advice and does not claim compliance with any law**: the numbers in it are design values, and a person qualified in the customer's country must review it and the draft notice at the end before any real customer (DT-127).

The patients in every case are made up, so no patient data is involved. The people whose data is held are the **trainees** (and the supervisors and administrators who use the service).

## What is held, where, who can read it, for how long

| Data | Why | Where | Who can read it | How long |
|---|---|---|---|---|
| Email address | Sign in, invitations, password reset | The sign-in service (Supabase Auth). Our own tables hold it only while an invitation is waiting (`invitation.email`) | The person; the team's platform administrators in the Supabase dashboard. **Never shown to supervisors or administrators of an organisation.** | Until the person deletes their data. A waiting invitation's address is removed when it is accepted or cancelled, and a month after it expired |
| Password | Sign in | Supabase Auth, stored hashed by Supabase | Nobody | Until the person deletes their data |
| Display name | So supervisors know who is who | `profile.display_name` | The person; supervisors and administrators of the same organisation | Until the person deletes their data |
| Pseudonymous id (`T-` and 8 characters) | Used in exports, reports and research instead of a name | `profile.pseudonym` | Same as the name; also appears in exports | Replaced by a new one when the person deletes their data |
| Membership, role, cohorts | Decide what a person may see | `membership`, `cohort_member` | The person; administrators of the organisation | Until the person deletes their data (an administrator can deactivate a person, which keeps the record) |
| Consent answers | Proof of what the person agreed to, which text, when | `consent_record` | The person; platform administrators | Until the person deletes their data |
| Session results: end state, result, process score, every score item with its evidence, the behaviour totals, the report | The point of the product | `session`, `session_score_item`, `session_behaviour` | The person; supervisors and administrators of the organisation (the whole organisation, DT-145). In a research export only if the person agreed, and then only under the pseudonymous id | Until the person deletes their data, after which they stay without any link to the person (so organisation totals stay correct) |
| Session recording (the log of actions) and the report page | To verify the session by replaying it | `session.log_gz`, `session.report_html_gz` | The person; the verification program; not shown in dashboards | 24 months by default; an organisation chooses 6 to 60 months; then removed by the daily clean-up |
| Access log: who opened whose report or progress, or exported results, and when | So a person can see who looked at them | `access_log` | The person it is about (their Privacy page); platform administrators | 24 months, or until the person deletes their data. When the *viewer* deletes their data, their name goes from the rows but the rows stay |
| Headset sign-ins: device name, hashes of the headset's tokens, last use | So a lost headset can be signed out | `device_session`, `device_login` | The person | Until revoked or the person deletes their data (clean-up of old ones: DT-142) |
| Learner profile | Reserved for the personalisation work (`p1-personalisation`) | `learner_profile` | The person; supervisors who may see them | Until the person deletes their data |
| Application logs | Find faults | The hosting provider's log | The team | The hosting provider's own period (to check, DT-155). Log lines carry ids, never names, emails or tokens: every line is cleaned before it is written and a test checks it |
| Backups | Recover from a mistake or a lost database | Planned (p1-backend-s13): copies of the application tables only | The team | To be stated when built (proposal: 30 days). A deleted person's data can remain in a backup until that backup expires |
| AI audit rows: for every AI request, the task, model and prompt version, a hash of the input, a reference to the output, the validator's result and reason codes, whether the fallback was used, the time it took | So what the AI said to a trainee can be traced afterwards, and the AI can be evaluated (`p1-backend-s05`) | `ai_call`. **No text**: no words a trainee said and nothing the AI said, only hashes, references and codes (a test checks it); the text stays in the headset's own session log | The person (`/organisations/{id}/ai-calls`, and in their data export); supervisors and administrators of the organisation (opening one person's calls is in the access log); the platform administrators see weekly totals | 12 months, then removed by the daily clean-up. When the person deletes their data the rows move to the anonymous stand-in, like the sessions |
| AI prompts | AI features (`p1-backend-s06`, built with the AI work) | Not built yet | The team | To be stated when built |

## Who may see an individual's data

- A **trainee** sees their own sessions, progress, access log and export, and nobody else's.
- A **supervisor** and an **organisation administrator** see every person's results in their own organisation. Opening one person's report, progress or session list, or exporting results, is written to the access log and the person can read it. The organisation overview (totals over many people) is not logged per person.
- Nobody sees another organisation. A platform administrator can export results for research, but only for people who agreed to research, without names or emails, with the organisation replaced by a code.
- The team's platform administrators can read the database (and the Supabase dashboard, where emails are). Access to it is limited to the team and is not a feature of the service.

## What a person can do

- **Choose:** at first sign-in, and again whenever the text changes, a person answers two questions: storing their results (needed to store sessions; without it uploads are refused) and research use (optional, can be changed any time).
- **Look:** the Privacy page lists who opened their data.
- **Take it with them:** "Download my data" gives one JSON file with the profile, memberships, consent answers, every session's results, score items and report, devices, the access log about them, and the AI audit rows of the calls made for them. The stored recordings of sessions are not in the file (DT-153).
- **Leave:** "Delete my data" (after typing DELETE) removes the sign-in account (and with it the email and password), the profile and name, memberships, consent answers, headset sign-ins and learner profile, and **cuts the link to their sessions**: the sessions move to a new empty profile with a new random id and a new pseudonym, so no table holds the old id, the name or the old pseudonym, and every organisation's totals stay exactly as they were. The sessions' results remain as anonymous history. It is refused for the only administrator of an organisation, who must hand that over first.

## What deletion does not do (stated honestly)

- A sign-in token issued before the deletion stays valid until it expires (at most an hour) because it is signed and not looked up; using it would create a new, empty profile that holds nothing.
- Backups (when they exist) are not edited: the deleted person's data disappears from them when the backup expires.
- Results kept as anonymous history are kept indefinitely. If a customer needs them to go after some years, that is a rule to add (DT-154).
- The hosting provider's own logs are outside the service's control (DT-155).

## Draft notice (for a qualified person to review; not yet in use)

> **ProcSense and your data.** ProcSense is a training simulator for hospital procedures. Your organisation uses it to train you and to see how training is going. We keep your name (as your organisation entered it), your sign-in email (held by our sign-in provider and never shown to your organisation), and the results of your training sessions. Your organisation's supervisors and administrators can see your results and who you are; you can see who looked. Results are kept until you delete your data; the recordings of your sessions are removed after the period your organisation chose (24 months unless it chose otherwise). With your optional agreement your results may be used, without your name or email, in studies to improve the training. You can download your data or delete your account at any time from the Privacy page. Contact: *(to be filled in)*.

## Terms for customers (to be drafted)

The terms between ProcSense and a customer organisation (who is responsible for what, what the customer promises its trainees, how a customer leaves and what happens to its data) are not written yet. They need a qualified person (DT-127).

## Semantic reader on Ollama Cloud (p1-ai-language-s18, ADR-0024), off by default

When `INTERPRET_LLM_ENABLED` is set, the transcribed sentence of a trainee, at most four earlier turns of the dialogue, the running scenario steps and the approved objective definitions are sent to Ollama Cloud (a third-party processor) to obtain a reading. Ollama states that it processes cloud prompts and responses to answer requests and does not use them to train models (https://docs.ollama.com/cloud); that statement has not been reviewed here. The service never sends a name, an identifier or a credential in the prompt, stores no text of the exchange (the audit row keeps a hash, reason codes, the model name and the rule-set version), and sends nothing for sentences that the local checks already refuse (critical topics, denials, plans, reports, quotations). The setting is OFF; enabling it needs a privacy and consent decision by a qualified person (DT-127, DT-248), and real trainee speech must not be sent before that decision.

## Per-task cloud routing (ADR-0026), off by default

`CLOUD_ROUTING_ENABLED` lets tasks whose route names an Ollama Cloud model (`content/ai/model_routes.json`) use it, with the local model as the fallback on any failure. Routes are classed `synthetic` (authored content only: scenario narrative, critic, judge, trap writer; no trainee data) or `trainee`. The `trainee` routes (patient dialogue, staff replies) send the trainee's transcribed question, up to four earlier turns and the facts the patient has earned; they stay on the local model unless `CLOUD_TRAINEE_DATA_APPROVED` is also set, which must not happen before the privacy and consent decision (DT-248, DT-127). The audit row keeps the route, the data class, the model and hashes, no text. Only synthetic text was sent during the benchmarks of 25 Oct 2026.

The same two gates cover the speech evaluator: its route answers UNAVAILABLE (reason `CLOUD_DATA_NOT_APPROVED`) and sends nothing when its model is a cloud model and neither `CLOUD_TRAINEE_DATA_APPROVED` nor `CLOUD_SYNTHETIC_DEPLOYMENT` is set. `CLOUD_SYNTHETIC_DEPLOYMENT` is the owner's declaration that a deployment handles invented data only; it is not the privacy approval and must not be set where real trainees play. `GET /ai/health` shows which gates are open.

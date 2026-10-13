# Data dictionary: the CSV export (p1-backend-s04)

Two exports, one set of columns. `GET /organisations/{id}/export.csv` is for a supervisor or an organisation administrator and covers their own organisation. `GET /export.csv` is for the platform administrator and covers every organisation (the evaluation study); in it the `organisation` column is a stable code and the organisation cannot be named.

Both take the same optional filters: `cohort_id` (organisation export only), `trainee_id`, `case_id`, `mode` (`practice` or `assessment`), `result`, `since` and `until` (a date and time; `since` is included, `until` is not). Only **verified** sessions are exported: a pending, rejected or unverifiable session is never a row. At most 200,000 rows are returned.

The file is UTF-8, comma-separated, one header row, `\n` line ends. A cell that starts with `=`, `+`, `-` or `@` is written with a leading `'` so a spreadsheet does not run it as a formula. There are no names and no email addresses anywhere in the file.

| Column | Meaning |
|---|---|
| `session_id` | The id the headset gave the session (a UUID). |
| `organisation` | Organisation export: its short name (slug). Platform export: `O-` and 8 characters, the same for every row of one organisation. |
| `trainee` | The pseudonymous id of the person (`T-` and 8 characters). It is the same across sessions and organisations, and cannot be turned into a name without the organisation's own records. |
| `case_id` | The case that was played, for example `VAR-S1-A`. |
| `mode` | `practice` or `assessment`. |
| `result` | The engine's verdict: `SUCCESS`, `SUCCESS_WITH_DELAY` or `FAIL`. It does not depend on the weights. |
| `end_state` | How the case ended, from the engine's end-state resolver. |
| `process_score` | The weighted process score over all behaviours, two decimals. **Provisional**: see the next column. |
| `process_score_provisional` | `true` while the weights the scorer multiplies by are provisional design values (they are today). The verdict (`result`) does not depend on them. |
| `credits` | Points earned over all behaviours. |
| `penalties` | Points lost over all behaviours (a positive number). |
| `critical_items` | How many critical score items the session had (any critical item fails the case). |
| `content_version` | The version of the content package the session was played with (twelve characters of the content hash, then the mode). |
| `engine_version` | The version of the engine that produced the log. |
| `received_at` | When the server received the upload (UTC, ISO 8601). |
| `verified_at` | When the engine replay confirmed it (UTC, ISO 8601). |

Not in the export on purpose: names, email addresses, the log, free text, and the individual score items (they are in the session report for a person who may see that session).

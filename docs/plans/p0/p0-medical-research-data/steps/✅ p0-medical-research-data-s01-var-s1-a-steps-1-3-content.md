# p0-medical-research-data-s01 — VAR-S1-A content for Steps 1–3

Walking skeleton 1 · step 1 of 15

## What
Write the first real content file — patient **VAR-S1-A (Mrs Ayesha Khan)** — with only what Steps 1–3 need: identity, wristband and
notes identifiers, allergies, reason for admission, history answers, baseline vital signs. Every value gets a row in the source register.
Also create the JSON schema the loader will validate against.

## Why
The engine must never contain clinical numbers (INSTRUCTIONS §4d, Implementation Guide "never type clinical values into code").
Starting with one real, fully traced case proves the content → engine path and the proof-of-truth process on a small scale.

## How to implement
1. `content/schema/case.schema.json` (JSON Schema draft 2020-12): `id`, `walkthrough_label`, `scenario`, `identity`, `wristband`,
   `notes`, `allergies[]`, `reason_answer`, `responsible_adult`, `timing`, `history[]` (`topic`, `answer`, `disclosure: "ask"`),
   `baseline_vitals`, and a `refs` map (field path → source-register ID). Unknown fields rejected.
2. `content/cases/VAR-S1-A.json` — transcribe **exactly** from walkthrough Part 12, Example Patient A (page 104):
   - Identity: Ayesha Khan, 42, teacher, DOB 14/03/1984; wristband text `AYESHA KHAN`, `14/03/1984`, `MR 102938`; notes match.
   - Allergies: none. Reason: "To have my gallbladder taken out with keyhole." Responsible adult: husband, takes her home and stays overnight.
   - Timing: time now 08:00; surgery 09:00.
   - History answers (topics: main complaint/pain, red flags, other illnesses, medicines, herbal, previous anaesthetic, family anaesthetic,
     smoking, alcohol, last food, last drink, pregnancy) — wording as in the walkthrough.
   - Baseline vitals: HR 76 regular; BP 124/78; RR 14; SpO2 98 % room air; temperature 36.7 °C; alertness Alert; pain 0/10.
3. `content/devices.json` — device timing used by Step 3: NIBP cycle "~30–40 s" (walkthrough Part E, BP cuff row).
4. Source register rows (`docs/research/source_register.csv`), IDs `SR-0001…`: one per value; `walkthrough_ref` = part + page;
   `evidence_type` = D for case values (walkthrough-authored simulator case values), status `VERIFIED` once re-read against the page;
   `notes` = "case value authored in walkthrough".
5. Log conflict in `docs/research/issues.md`: **NIBP cycle** — walkthrough Part E "~30–40 s" vs Implementation Guide L3-02 "BP value after 20 s sim".
   Walkthrough used; decider: medical professional / team.
6. A tiny validation script `tools/content/validate.py` (run with `uv run`) that checks every case file against the schema and that every
   `refs` ID exists in the register.

## Verification
- `uv run tools/content/validate.py` → "VAR-S1-A: valid, 0 missing refs".
- Spot check: open walkthrough page 104 next to `VAR-S1-A.json`; every value identical.
- `grep` the register: every ref ID in the case file exists.

## Files changed
`content/schema/case.schema.json`, `content/cases/VAR-S1-A.json`, `content/devices.json`, `docs/research/source_register.csv`,
`docs/research/issues.md`, `tools/content/validate.py`, `tools/pyproject.toml` + `uv.lock`, `Taskfile.yml` (`task content-validate`).

## Dependencies
None (first step). Needs `uv` installed for the validator (fallback: run validator later; JSON files can be written now).

## Common issues
- Date formats: keep the wristband **display text** separate from the machine date (`1984-03-14`) so the UI shows it as written.
- Don't "improve" wording of answers — transcription only; the walkthrough is the source.
- BMI/airway/LFT values exist on the same pages but belong to later steps — leave them out now.

## Sources
Walkthrough Part 12 Example Patient A (p.104), Part E BP cuff row (p.201); docs/research/README.md (proof-of-truth protocol); INSTRUCTIONS §4d.

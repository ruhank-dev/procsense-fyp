# Labelling guidelines

For the human-labelled sets of p1-ai-infrastructure-s04. The tool is `tools/labelling/label.py` (sheets, agreement, adjudication, frozen splits); the tests are `tools/ci/test_labelling.py`. Nothing here labels anything itself.

## Roles and rules

- **Two labellers** fill separate sheets without seeing each other's. Each types their own name in the `labeller` column; the two must be different people. **A third person** settles every disagreement and is never one of the two.
- A set can be **frozen** only when it has the planned number of rows and **kappa is at least 0.6** (quadratic-weighted for ratings). Frozen files are split by group (speaker, author or case), written with their hashes, and a result is valid only for those hashes.
- **Weekly agreement check** while labelling goes on: run `agreement` on the week's items; if kappa drops below 0.6 stop, discuss the disagreements, and update these guidelines before continuing (labeller drift).

## The five kinds

| Kind | Label set | Needed to freeze | Guide |
|---|---|---|---|
| `utterance` | the 20 acts of `content/assessment/taxonomy.json` | 300 rows | [classifier_benchmark/annotation_guide.md](../classifier_benchmark/annotation_guide.md): one label per sentence, tie-break order |
| `topic` | the history topics of `content/behaviour` for a free question | 100 rows | the topic the question asks about; a question about two topics gets the first |
| `scenario` | a quality rating 1 to 5 | 100 scenarios | 5 = a clinician could teach with it as it stands; 3 = usable with edits; 1 = unsafe or incoherent. Rate the story, the numbers and the pictures together. |
| `relation` | correct, wrong, unclear | 100 relations | judge a knowledge-base relation (`content/kb`) against the quoted source line only; unclear if the quote does not settle it |
| `persona` | consistent, inconsistent, unclear | 100 lines | does a line the patient said fit the persona tags of the case (age, education, anxiety, talkativeness)? |

## Steps

1. Make the items file (JSON lines, each with `id`, `text` and a `group`), then `label.py sheet --kind K --items items.jsonl --out sheet.csv`. Give each labeller a copy.
2. When both are back: `label.py agreement --a a.csv --b b.csv --kind K --out disagreements.csv`. It prints the agreement and kappa and lists the disagreements.
3. The third person fills `label` and `adjudicator` in `disagreements.csv`; `label.py adjudicate --a a.csv --b b.csv --adjudication disagreements.csv --kind K --out final.csv`.
4. `label.py freeze --final final.csv --items items.jsonl --kind K --out-dir tests/labelled/K --kappa <the printed kappa>`.

Role-play recordings follow [consent_and_storage.md](consent_and_storage.md).

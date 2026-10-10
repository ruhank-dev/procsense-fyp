# content/

The medical content package the Core engine reads: vital-sign and lab bands, NEWS2 tables, WHO checklist,
cases, what-ifs, medicines (as plans, never trainee doses). **No clinical number is ever typed into code.**

Rule: every value here has a matching row in `docs/research/source_register.csv` (evidence type A/B/C/D +
verification status). Rebuilt from the walkthrough in a later plan step.

## Tools
- `task content-validate`: every content file against its schema and the source register (cases, rules, workflow, CSV tables need a `ref` column).
- `task content-extract`: parses the walkthrough into `content/_extract/walkthrough_extract.json` (git-ignored drafting aid; never trust a drafted number without re-reading the page) and checks the counts.
- `task content-trace`: regenerates `docs/research/traceability.md` (which walkthrough part feeds which file and register rows).

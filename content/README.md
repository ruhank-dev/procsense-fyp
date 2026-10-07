# content/

The medical content package the Core engine reads: vital-sign and lab bands, NEWS2 tables, WHO checklist,
cases, what-ifs, medicines (as plans, never trainee doses). **No clinical number is ever typed into code.**

Rule: every value here has a matching row in `docs/research/source_register.csv` (evidence type A/B/C/D +
verification status). Rebuilt from the walkthrough in a later plan step.

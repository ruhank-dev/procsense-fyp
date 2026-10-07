# p0-foundation

**Tier:** P0 · **Builds on:** — · **Feeds:** every phase · **Status:** step s00 ✅ approved

## Goal
A repo anyone can clone, build and test in one command, with conventions that keep the team (and Claude) consistent — and backed up so data can't be lost again.

## Scope
- **In:** folder layout, .NET solution, Taskfile, git conventions, CI, code style, ADR + experiment templates, `.memory/` log,
  remote backup, tool installs (Task, uv, Unity 6, Blender, Git LFS).
- **Out:** feature code.

## Deliverables
- Repo skeleton — `steps/✅ p0-foundation-s00-repo-foundation.md` (approved 07 October 2026).
- Tools installed and documented in README.
- Remote repo + Git LFS for binary assets.
- CI: build + test Core on every push; later Unity build check and Blender asset validation.
- Templates: ADR, experiment record, asset spec sheet.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Hosting / backup | GitHub private repo | GitLab; university server; local only | Free CI, common | University requires another host |
| Binary assets (.blend, .fbx, textures, audio) | Git LFS | Commit directly; shared drive | Keeps repo fast; versioned assets | LFS quota exceeded → generated assets rebuilt from scripts (scripts are the source; outputs can be untracked) |
| CI | GitHub Actions | Local only | Catches breakage early | — |
| Branching | `main` + short feature branches + PR | Trunk-only; GitFlow | Simple for a small team | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-FND-1 | Should generated Blender outputs be committed or rebuilt in CI? | Measure output sizes + Blender headless time in CI | Pick cheaper option that keeps builds reproducible |

## Exit
Fresh clone on a second machine → build + test green; CI green on `main`.

## Who does what
Claude: all, except installs/accounts needing your login (GitHub, Unity ID).

## Sources
Master Plan §25; Implementation Guide B-S11.

# Data tools: fetching, rebuilding and what is committed

**Short version.** Everything the app, the tests and CI need is **committed**: the pictures (`content/images`, `content/ecg`, `content/cxr`, `content/xsec`), their reports, the tables, the
imaging library, the dataset register and cards. A teammate who just clones the repo needs to download nothing. The big source datasets are only needed to **rebuild** the pictures or to
change which source images are used. They are not in git (several GB, and some licences do not allow redistributing the raw files); one command fetches exactly the parts used.

## Fetch the source data (only to rebuild)

```
task data-fetch      # downloads what is missing, then verifies it (about 3 GB the first time, resumable)
task data-check      # verify only, no network
task data-rebuild    # rebuild every generated picture, report, table and register from the fetched data
```

Without Task: `uv run --project tools --extra images tools/data/fetch_all.py` (add `--list` to see what it will fetch and why, `--check` to verify only).
Needs `uv`; the first run installs Pillow, numpy, pyarrow and nibabel.

`fetch_all.py` downloads exactly this, into `tools/data/cache/` (git-ignored), and checks every file's size and SHA-256 against `fetch_manifest.json`:

| Data | Size | Used for | Licence |
|---|---|---|---|
| UIdataGB, 5 class archives (Mendeley) | ~0.9 GB | real gallbladder ultrasound pictures | CC BY 4.0 |
| AMOS 2022, 9 scans with labels (Zenodo, read out of the 24 GB archive by range request) | ~0.2 GB | CT and MRI base slices | CC BY 4.0 |
| NIH ChestX-ray14, 5 shards of the Hugging Face copy | ~2.2 GB | real chest films | NIH: no restrictions, attribution required |
| PTB-XL metadata and the recordings named in `content/ecg/index.json` | ~10 MB | real ECG strips | CC BY 4.0 |

The aggregates built from VitalDB, PTB-XL beats and the MIMIC demos (`tools/data/derived/*.json`) are committed too; to recompute them run `extract_vitaldb.py`, `extract_ptbxl.py` and
`extract_mimic.py` (each downloads its own source). The ECG builder downloads any further PTB-XL records it needs by itself.

## What each script does

| Script | Writes |
|---|---|
| `ultrasound.py build` (+ `ultrasound_synth.py`) | `content/images/**`, `content/imaging_reports.json` (real UIdataGB pictures chosen in `ultrasound_real_selection.json`, the rest drawn in code) |
| `ecg_images.py` | `content/ecg/**`, `content/ecg_reports.json` (real PTB-XL strips; potassium and fast-AF patterns edited from real ones) |
| `cxr_images.py build` | `content/cxr/**`, `content/cxr_reports.json` (real NIH films chosen in `cxr_real_selection.json`; free air painted into real films) |
| `xsec.py build` | `content/xsec/**`, `content/xsec_reports.json` (real AMOS slices; findings painted in; MRCP drawn in code) |
| `approvals.py grant` | `content/image_approvals.json` (owner-attested approvals tied to each exact picture; rebuilds re-apply them) |
| `dataset_cards.py` | `docs/research/dataset_register.csv` and `docs/research/datasets/*.md` |
| `../content/imaging_library.py` | `content/imaging_library.csv` (every result and its pictures; checks coverage) |

Pictures are deterministic from their seeds and the fetched sources. A rebuild on another machine can differ in a few pixels if the image library version differs; if it does, CI's picture
check (`validate_images.py`) will say which picture no longer matches its recorded hash, and the committed version stays the reference.

## Attribution and licences
Every real picture carries its source, licence and attribution in its index entry; every synthetic one carries the visible mark "SIMULATION: synthetic image, not a real scan", its generator
and seed (and the real base it was made from). Cards: `docs/research/datasets/`. Decisions: `docs/decisions/ADR-0003-synthetic-medical-images.md`, `ADR-0004-imaging-library.md`.

# DS-0014 AMOS 2022: abdominal CT and MRI

**Source:** https://zenodo.org/records/7155725 · **Licence:** CC BY 4.0 · **Cite:** Ji Y. et al., AMOS: A Large-Scale Abdominal Multi-Organ Benchmark for Versatile Medical Image Segmentation, NeurIPS 2022 Datasets and Benchmarks, doi:10.5281/zenodo.7155725.
**Checked:** 2026-10-10: the archive index was read (600 CT and MRI scans with 15-organ labels); ten scans were downloaded one member at a time (`tools/data/fetch_amos.py`, about 15 MB per CT, 3 MB per MRI) and looked at.

## Purpose
CT and MRI results for the appendix level, the gallbladder level and the clean (no problem) result of each test. No open dataset with matching reports exists for these tests (docs/research/imaging_scenarios.md).

## Method (`tools/data/xsec.py`)
- **Clean pictures:** a real slice, cropped, window-levelled (CT soft-tissue W400 L40) and re-encoded without metadata; 16 used.
- **Problem pictures:** the same kind of real slice with the finding painted in (inflamed appendix with fat stranding, abscess with gas bubbles, gas-containing thickened gallbladder wall, enhancing appendix wall on MRI) at the size the report states, using the scan's own voxel size and noise. Flagged synthetic, base slice named, visible SIMULATION mark. Because the base is real, clean and problem pictures look alike.
- The gallbladder wall painting is placed from AMOS's own gallbladder label.

## What ships
56 pictures in `content/xsec/` with attribution, plus the 6 MRCP pictures drawn in code.

## Limits
The patients have many conditions; slices were chosen by eye and the clinician confirms they show no abnormality in the region. The MRI sequence is not stated by the dataset for each scan. MRCP is clearly diagrammatic (drawn in code), the least realistic part of the library.

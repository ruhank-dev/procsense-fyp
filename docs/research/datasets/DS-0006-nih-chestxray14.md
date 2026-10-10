# DS-0006 NIH ChestX-ray14: chest X-rays

**Source:** NIH Clinical Center, https://nihcc.app.box.com/v/ChestXray-NIHCC · **Cite:** Wang X., Peng Y., Lu L., Lu Z., Bagheri M., Summers R., ChestX-ray8, IEEE CVPR 2017, pp. 3462-3471.
**Licence:** NIH states no restrictions on use with attribution (link to the NIH site, cite the paper, acknowledge the NIH Clinical Center). This was read in the Google Cloud and RSNA documentation because the NIH page is script-driven; the Hugging Face copy used for download states no licence. **Re-confirm on the primary page before release.**
**Checked:** 2026-10-10: one parquet shard (test-00000-of-00010, 450 MB, about 1,100 images) downloaded and read; contact sheets looked at.

## What we need from the 45 GB
Almost nothing. Each finding needs a few pictures, so one shard is enough. The 45 GB is only the full collection (112,120 images). The original download site offers `Data_Entry_2017_v2020.csv` (labels) and `images_001.tar.gz` to `images_012.tar.gz`; the Hugging Face copy splits the same images into 93 parquet shards.

## Method (`tools/data/cxr_images.py`)
Images with exactly one label and the PA view; seven finding codes (CXR-01 to CXR-07) of real films plus CXR-08 (free air under the diaphragm, painted into a real film, flagged synthetic and marked); four pictures each chosen by eye; resized to 640 x 640 and re-encoded from pixels (no metadata). Patient id, age and sex are not copied.

## What ships
32 pictures in `content/cxr/` with reports that state only what the label supports. The dataset has no radiology reports.

## Limits
Labels were text-mined from reports and are not clinician-checked (some pictures may not show the labelled finding clearly). The book has no chest X-ray report categories, so the codes are proposals. Approved for training on the owner's statement (see `content/image_approvals.json`).

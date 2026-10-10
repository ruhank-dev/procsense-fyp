# DS-0005 UIdataGB: real gallbladder ultrasound images

**Source:** https://data.mendeley.com/datasets/r6h24d2d3y/2 · **Licence:** CC BY 4.0 · **Cite:** Turki A., Obaid A.M., Bellaaj H. et al., Gallbladder Diseases Dataset, Mendeley Data V2, doi:10.17632/r6h24d2d3y.2
**Checked:** 2026-10-10: file list and licence read; the class archives used were downloaded (`tools/data/fetch_uidatagb.py`) and each matched its published SHA-256.

## Purpose
Real images next to the matching reports in the EMR viewer.

## Method
Classes are mapped to the book's finding codes only where the match is clear: Gallstones to US-GB-01; cholecystitis to US-GB-02; membranous and gangrenous cholecystitis and perforation to US-GB-05; carcinoma to US-GB-06. The dataset has no images for bile-duct findings (US-GB-03, US-GB-04) or sludge only (US-GB-07). A few images per class were picked from a contact sheet checked by eye for burned-in text; each is downscaled to 640 x 480 and re-encoded as JPEG, which removes EXIF and ICC data.

## What ships
16 images in `content/images/`, listed in `content/images/index.json` with their archive, member, licence and attribution; reports in `content/imaging_reports.json`.

## Limits
The dataset labels were not checked by a clinician for this project, and a class does not prove a measurement. Every image is shown only at the status recorded in `content/image_approvals.json` (approved for training on the owner's statement that the clinician approves everything; the clinician is not named). Attribution is required wherever an image is shown.

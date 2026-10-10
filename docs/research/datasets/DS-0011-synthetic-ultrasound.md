# DS-0011 Synthetic ultrasound set

**Generator:** `tools/data/ultrasound_synth.py` (stylised procedural illustration, deterministic from a seed) · **Decision:** ADR-0003

## Purpose
A labelled stand-in picture, with a matching report, for finding codes with no suitable real image: US-AP-01, US-AP-02, US-AP-03, US-AP-04, US-AP-05, US-GB-00, US-GB-03, US-GB-04, US-GB-07, US-GB-08, US-PV-00, US-PV-01, US-PV-02, US-RN-00, US-RN-01.

## Method
A fan-shaped field with speckle texture, a simple anatomy sketch and the finding drawn at the size the report states; the calipers in the picture show the number the report uses. The mark "SIMULATION: synthetic image, not a real scan" is drawn into every picture and CI re-reads it from the pixels.

## What ships
60 images in `content/images/`, flagged `synthetic: true` with generator and seed.

## Limits
These are teaching illustrations, much simpler than a real scan; not a trained generative model; never used to train or test any diagnostic model. Approved for training on the owner's statement (see `content/image_approvals.json`).

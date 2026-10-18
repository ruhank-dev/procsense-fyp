# Local drug availability: sources, rules and what is built (p2-content-extensions-s04)

Status on 18 October 2026: **the architecture is built and tested; live availability is off because no source that permits this use has been established.** Nothing in the product says a medicine is or is not in stock.

## What was checked

| Source | What it is | Stock? | Reuse terms | Decision |
|---|---|---|---|---|
| DRAP registered pharmaceutical and biological drug products database (official) | Registration: brand, form, composition, registration number and date, holder, manufacturer | **No** | Public version "for reference purposes only", "subject to a disclaimer", no advice (R-042); on 17 Oct its own terms were read as excluding citation and research | Excluded. Registered does not mean stocked |
| DRAP medicine availability page (official) | Entry point for public and hospital shortage reports, plus links to the registers | Reports, not a dataset | No dataset, API or reuse terms published; rights reserved | Excluded: nothing to read and no permission to reuse |
| A named hospital pharmacist's list | Would be real stock for one place | Yes | By agreement with that person | The supported route: `pharmacist_supplied` source |
| A licensed commercial or open feed | Not found | Unknown | Unknown | `licensed_feed` source kind exists for it |

No scraping, no undocumented endpoint, no inference from registration.

## What exists

* `server/availability/provider.py`: an `Availability` answer carries status (`available`, `unavailable`, `unknown`), source, source kind, UTC observation time and maximum age. Rules in the type: a `registration_register` source cannot hold a stock status; synthetic answers are never live.
* `server/availability/sources.py`: `SyntheticProvider` (invented values in `content/availability/synthetic_demo.json`, for demos and tests, served only in demo mode and always labelled) and `FileProvider` (a CSV from a source listed in `content/availability/permitted_sources.json`; rows are refused with a reason when the source is unlisted or does not allow teaching display, the status is unknown to the code, the date is not an ISO date or lies in the future).
* `server/availability/service.py`: `resolve()` applies the switch (`live_enabled`), freshness (older than `max_age_days` becomes `unknown` with the reason) and demo mode; `note()` gives the one sentence a debrief may add (only for "unavailable" from a real source; synthetic values are prefixed so they cannot pass as real).
* `content/availability/medicine_availability.csv`: one row for each of the 41 medicine groups of `content/medicines.csv`, built by `tools/content/build_availability.py`, every flag `unknown` with source `none` and the date checked. A flag other than `unknown` must name a listed permitted source (checked in CI).
* Tests: `tools/ci/test_availability.py` (10 tests).

Nothing calls `note()` yet: with no permitted source it would always return an empty string, so the debrief text is unchanged. When a source is added the call belongs in the debrief explanation of a plan that uses the medicine.

## What a later source needs

1. A named source with written permission or published terms that allow displaying availability in a teaching tool.
2. An entry in `permitted_sources.json` (name, terms URL, allowed uses including `teaching_display`, kind, attribution) and a row in `licence_provenance_register.md`.
3. A CSV `medicine_id,status,observed_on,source_id` from that source, refreshed within `max_age_days`.
4. `live_enabled: true` in `content/availability/config.json`.

Who: the owner (or a named pharmacist at the teaching hospital) must supply or approve the source. No payment is needed to build any of this.

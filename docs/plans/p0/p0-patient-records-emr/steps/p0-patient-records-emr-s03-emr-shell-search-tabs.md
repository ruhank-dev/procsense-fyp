# p0-patient-records-emr-s03 — EMR shell: search, patient list, tabs

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Workstation UI on `SCREEN_EMR`: search by name / MRN / DOB, quick patient list, virtual keyboard, tabs across the top that open/close/switch (several open at once), consistent hospital-system look.

## Why
The computer is the electronic medical record for the simulation.

## How to implement
1. `Scripts/Records/Emr/EmrShell.cs`, `SearchView`, `TabBar`.
2. Pointer + poke input; quick list first because VR typing is slow.
3. Search ranks exact MRN > name prefix > DOB; results show name, MRN, DOB, location.
4. Tabs: max 6 open, close button, reorder not needed (YAGNI).
5. Screen visual style: plain hospital-system look (white/grey, clear headers) — readable over decorative.
6. Pointer = controller ray with poke fallback; keyboard is a world-space keypad.

## Verification
Play Mode test: search 'khan' → VAR-S1-A first; open Profile, Labs, Imaging tabs; switch; close one.

## Files changed
`Scripts/Records/Emr/*`.

## Dependencies
p0-patient-records-emr-s01.

## Common issues
Searching with the virtual keyboard is slow — the quick list must already show the current patient first.

## Sources
User direction (EMR with search bar, tabs, patient profile); p0-patient-records-emr README decisions.

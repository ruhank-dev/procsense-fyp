# p0-patient-records-emr-s02 — Physical folder interactions

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Pick up the folder, open the cover, flip pages with animated page turns, pick up / place reports in the pocket, read documents (live text), view printed images.

## Why
You asked for real folder interactions: opening it, flipping pages, picking up and placing reports — the physical folder is how current-episode paperwork is handled in a real ward.

## How to implement
1. Greybox folder first; Blender folder (p0-blender-equipment-s06) with page rig later.
2. `Scripts/Records/FolderController.cs`: page turn on grab-drag or button; pages render Core documents.
3. Page turning independent of frame rate.
4. Page content rendering: each page is a RenderTexture or TMP canvas bound to a Core document; long documents paginate.
5. Place report: drop on folder pocket `SOCKET_Report` → `file_document` action → becomes a page.
6. Grab-drag page turn with a threshold; button fallback for accessibility.

## Verification
Play Mode test: open folder, turn 3 pages, file a report; in the headset page turns feel natural (EXP-REC-3 later).

## Files changed
`Scripts/Records/Folder*`.

## Dependencies
p0-patient-records-emr-s01.

## Common issues
Page meshes clipping through each other while turning → turn one page at a time with a short lock; text unreadable at an angle → snap the folder to a reading pose when held.

## Sources
User direction (folder interactions); Implementation Guide B-S1 Step 7 (documents as assets); walkthrough Part E (#5 notes/folder, #7 consent form).

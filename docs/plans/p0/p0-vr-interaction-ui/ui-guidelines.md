# UI guidelines: readability and accessibility (p0-vr-interaction-ui-s10, EXP-UI-2)

These rules hold for every world-space panel ProcSense makes. They are checked by `unity/Assets/Tests/PlayMode/AccessibilityTests.cs`, and the numbers live in `unity/Assets/Scripts/UI/Accessibility/Readability.cs`.

## Text size

- **Minimum: 24 headset pixels tall at the distance the panel is read from.** This is the plan's "24 px at 1 m". The Quest 3 shows about 25 pixels per degree, so 24 px is about 0.96° of view. That is roughly 17 mm of text height per metre of distance.
- **Reading distances.** Each panel has one, in `AccessibleCanvas.Distances`; a panel not listed there is read from 1 m.

  | Where the panel is | Reading distance | Panels |
  |---|---|---|
  | Held in the hand | 0.45 to 0.5 m | Tablet, phone, folder pages, printouts, wristband card. The specimen label is read from 0.3 m. |
  | At a desk or trolley | 0.6 m | EMR, results, diagnostics preview, spoken-order panel, menus, waiting status |
  | At the bedside | 1 m | Monitor, subtitles, event cards, ward checklist, debrief |
  | On a wall | 1.2 to 1.5 m | WHO board (1.5 m), doors, diagnostic room signs |

- **Enforced in code.** `UiKit.Label` raises any font below its panel's floor to the floor (`AccessibleCanvas.MinFontSize`). A new panel therefore cannot ship text that is too small. Wall panels were made physically larger instead of cramming bigger text into the same space:
  - the WHO board is now 1.43 m wide,
  - the ward checklist is 0.9 m,
  - the debrief screen is 1.4 m.
- **Viewer's panel size.** On the tablet, Settings → Panel size sets 100 %, 125 % or 150 %. It scales every panel at once and only ever makes text bigger.
- **Viewer's subtitle size.** Settings → Subtitle size sets 100 %, 130 % or 160 %, on top of the panel size.
- **Text rendering.** Use TextMeshPro SDF text only; it stays sharp in the headset.
- **To check on the device.** Dynamic resolution can soften text, so check legibility on the device itself. That is the external check UX-P0-VRUI.

## Contrast

- **WCAG AA (4.5 : 1) for every text colour on its background.** The test computes the WCAG 2 ratio for these pairs:
  - text on a panel,
  - dim text on a panel,
  - warnings on a panel,
  - button labels,
  - the four monitor zone colours,
  - the pleth trace on black.
- **Disabled buttons are exempt.** WCAG 1.4.3 does not apply to inactive controls.

## Status is never shown by colour alone

- **Monitor.** Each value has a zone word beside it (watch, worry, EMERGENCY). Each alarm carries a symbol and a word (!!! HIGH, !! MEDIUM, ! LOW). An alarm that has been silenced says so.
- **Results.** Flags appear as letters as well as colours.
- **Checklists.** Item states are written out, for example "[done]".
- **Debrief.** Each item has a severity word (CRITICAL, MAJOR, MISSED, CREDIT).

## Hands and seated play

- **Hand choice.** Settings → Hand swaps the sticks. Right-handed (the default): the left stick walks, and the right stick turns and scrolls. Left-handed: the other way round. The rig is rewired at once through `RigLocomotionSettings`.
- **Seated play.** The rig detects seated play from head height (`ComfortControls`). The Settings tab and the comfort panel can switch it either way. It is not saved, because the head height is measured again each time.
- **Saving.** Panel size, subtitle size and hand choice are saved on the headset with PlayerPrefs. "Reset for the next person" clears them.

## Still to do with people (EXP-UI-2)

- Try 18, 24 and 32 px text with 3 users, at 1 m and at 0.6 m, in the headset.
- If the minimum changes, change `Readability.MinHeadsetPixels`; every panel follows.
- This is part of the UX-P0-VRUI check (`docs/testing/SANNAN_EXTERNAL_TESTING.md`).

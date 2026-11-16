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

## P3 additions (P3-UIX-08, 11 Nov 2026)

These rules extend the ones above for the P3 screens. The checks live in `Readability.cs`; `UiThemeTests` and `HeadLockedRulesTests` run them.

### Text size for the new screens

- **One type scale.** The P3 screens use the type sizes of the design tokens (`docs/plans/p3/ui-design-tokens.md`): title 48, heading 32, body 24 and caption 18. These are canvas units as seen from 0.6 m.
- **Constant angular size.** A panel placed further away uses a proportionally larger canvas scale, `UiTheme.MetresPerUnit(distance)`. Text then keeps the same angular size at every distance: a caption is about 26 headset pixels whether the panel is on the wrist or 1.6 m away.
- **Why.** A fixed scale makes body text on a 1.2 m menu about 17 pixels, under the 24-pixel minimum.

### Distances

| Placement | Distance |
|---|---|
| Menus | 1.2 to 1.6 m |
| Contextual panels | 0.6 to 1.2 m |
| Wrist UI | 0.35 m |
| Nothing closer than | 0.3 m |

### Content that follows the head

Only small things follow the head: subtitles, toasts, the listening ring and the waiting status. Menus and panels stay in the world. Content that follows the head must be:

- **Far enough:** at least 0.75 m away, so the eyes don't strain to converge on it.
- **Close to the line of sight:** between 25° below and 15° above it, and within 20° to either side.
- **Narrow enough:** no wider than 40°, so a line can be read without turning the head.

The subtitle line was 48° wide at 0.95 m, so it was narrowed to 0.63 m (700 units, about 37°) and allowed one more line.

### Contrast on glass

- **The rule.** Every text colour must reach 4.5:1 on the glass panel over each background a room can put behind it: the sunlit window, a dark doorway, the grey floor and the warm wall.
- **What it takes.** That is why the glass is 85 % opaque and red text uses `#FF8589` (see the tokens page). Labels on accent buttons use the dark text colour.

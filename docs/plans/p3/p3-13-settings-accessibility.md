# p3-13-settings-accessibility — Settings, comfort and accessibility (SET)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

Settings open from the main menu, the pause menu and the wrist menu, in the same glass style. Tabs: **Interaction** (menu-based or voice-based, voice feedback on/off, talk button), **Audio** (master, voice, music, sound effects, ambience), **Subtitles** (on/off, size, background), **Comfort** (snap or smooth turning, snap angle, turn speed, comfort vignette, teleport or continuous movement, seated mode and height), **Display** (panel size, high contrast, reduce motion, dominant hand), **Session** (return to main menu, quit). Changes apply at once and are remembered next time. Switching interaction mode mid-session never resets or changes progress.

## 2. Current implementation status

`SettingsTabView` (tablet "Settings" tab: panel size, subtitle size, which hand walks, seated/standing, reset), `AccessibilitySettings`, `AccessibilityApplier`, `AccessibleCanvas`, `ComfortControls`, `ComfortPanel`, `RigLocomotionSettings`, `LocomotionFade`. No audio mixer or volume settings, no interaction-mode switch, no subtitle toggle, no vignette option, no settings outside the tablet. **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

All of the above; `PlayerPrefs` (already used for accessibility choices); XRI snap/continuous turn and teleport providers on `PlayerRig`.

## 4. Missing functionality

A single settings model with versioned persistence; the full panel; audio mixer and groups; the interaction-mode switch; vignette; subtitle toggle and background; high contrast and reduce motion; return/quit with the correct session semantics.

## 5. Architecture and integrations

`GameSettings` (plain C#, JSON in `PlayerPrefs` with a schema version; migration from the current keys) exposes typed values and change events; appliers (audio, comfort, accessibility, interaction mode) subscribe. Per-session values are not stored in the engine; the session header records the interaction mode and comfort mode for analysis only.

**Persistence:** all settings persist per device except "Return to main menu" and "Quit", which are actions.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-SET-01 | `GameSettings` model with versioned JSON persistence and migration of the current accessibility keys | Moiz | — | EditMode tests: defaults, migration, round trip | Planned |
| P3-SET-02 | Settings panel with the six tabs, reachable from menu, pause and wrist | Moiz | P3-SET-01, P3-UIX-04 | Every control changes its value live | Planned |
| P3-SET-03 | Audio mixer groups (master, voice, music, SFX, ambience, UI) wired to sliders | Moiz | P3-SND-01, P3-SET-02 | Slider changes the group's level (test) | Planned |
| P3-SET-04 | Interaction-mode switch mid-session (calls `InteractionMode`, P3-VOX-02) with a one-line explanation of what changes | Moiz | P3-VOX-02, P3-SET-02 | State hash unchanged by switching (test) | Planned |
| P3-SET-05 | Comfort: snap/smooth turn, angle, speed, vignette during movement, teleport/continuous, seated height | Sannan | P3-SET-02 | Each option works on the rig in PlayMode | Planned |
| P3-SET-06 | Accessibility: subtitle on/off, size, background plate, high contrast theme variant, reduce motion (disables non-essential tweens), dominant hand | Moiz | P3-SET-02, P3-UIX-08 | Each option visible in captures | Planned |
| P3-SET-07 | Session tab: Return to main menu and Quit with the confirmations of P3-MNU-09 | Moiz | P3-MNU-09 | Same semantics as the pause menu | Planned |
| P3-SET-08 | Tests: persistence, live application, mode switch invariance | Moiz | P3-SET-04, P3-SET-06 | Green in CI | Planned |

## 7. Dependencies and execution order

Model → panel → appliers (audio after the mixer, mode switch after VOX) → session tab → tests.

## 8. Frontend components

Settings panel, tabs (segmented control), sliders, toggles, dropdown for snap angle, confirmation dialogs.

## 9. Backend and API changes

None (settings are local to the device).

## 10. AI and voice integration

The interaction-mode switch and "voice feedback" toggle (spoken failure phrases on/off; subtitles always on for failures).

## 11. Unity objects, prefabs, animations and assets

Settings prefab; `ProcSense.mixer` audio mixer asset (P3-SND-01); vignette mesh/material (stereo-safe).

## 12. Performance

Vignette is one mesh in front of the camera with a stereo-safe unlit shader, only during movement.

## 13. Error handling and edge cases

Corrupt saved settings (reset to defaults with a message); voice mode chosen while the server is down (warning, still allowed, falls back per P3-VOX-10); seated mode change while moving.

## 14. Testing

EditMode and PlayMode as above; headset comfort in DC-14.

## 15. Acceptance criteria

1. Every listed setting exists, applies live and persists.
2. Switching interaction mode never changes the session state.
3. Return and quit behave as their confirmations say.

## 16. Definition of Done

All SET tasks Done and tested; comfort options checked on the headset.

# p3-12-ui-design-system — One glass design system that works on Quest (UIX)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented.

## 1. Objective and final user-facing behaviour

Every screen and panel looks like it belongs to one product: floating, softly frosted glass with a fine bright edge, a gentle highlight that moves as the head moves, clean modern type, clear icons, generous spacing, and calm motion. Panels sit in the hospital world at comfortable distances and sizes, are easy to hit with the controller ray, and stay readable on Quest 2. The look is achieved without screen-space blur or refraction, so it costs almost nothing on the headset.

## 2. Current implementation status

`UiKit.cs` builds flat world canvases with TextMeshPro labels and buttons; colours are constants (`WarnColor`, `DimTextColor`); `RoundedGraphic` exists for the login; readability rules exist as numbers (`Readability.cs`, `docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md`); `AccessibleCanvas` scales panels. **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

`UiKit` (keep its factory API so existing views restyle without rewrites), `Readability`, `AccessibleCanvas`, `WorldUiRaycaster`, `RoundedGraphic`, `LoginArtwork`, TextMeshPro, XRI UI input module, baked reflection probes (for the glass sheen).

## 4. Missing functionality

Design tokens; a Quest-safe glass shader; a component kit; icons; motion presets; migration of all existing views; desktop-only extra quality.

## 5. Architecture and integrations

**Tokens (`UiTheme` ScriptableObject + `docs/plans/p3/ui-design-tokens.md` written in P3-UIX-01):**

| Token | Value (initial) |
|---|---|
| Glass tint | deep blue-grey `#0F1A24` at 62 % opacity; light variant `#E8F0F6` at 18 % |
| Edge | 1.5 px equivalent, white at 35 %, brighter at the top edge |
| Accent | teal `#2EC4B6` (actions), amber `#F2B33D` (hints), red `#E5484D` (danger/critical), green `#3CCB7F` (done) |
| Text | primary `#F4F7FA`, secondary 70 %, disabled 40 % |
| Type | Inter (SIL OFL) or the TMP default if licensing is not confirmed: title 48, heading 32, body 24, caption 18 (canvas units at 0.0006 m/unit; matches the minimum angular sizes in `Readability.cs`) |
| Radius / spacing | 24 / 8-16-24-32 |
| Motion | open 180 ms ease-out, close 120 ms, hover 60 ms, tick 250 ms |
| Distances | menus 1.2-1.6 m; contextual panels 0.6-1.2 m; wrist UI 0.35 m; nothing closer than 0.3 m |
| Targets | buttons at least 64 × 64 canvas units (about 2.2° at 1 m), 12 units apart |

**Glass shader ("ProcSense/UI Glass", URP, unlit, transparent, one pass):** rounded-rectangle signed-distance mask from UVs (no texture), tint, a frosted gradient sampled from the room's **baked reflection probe at a high mip level** (looks blurred, costs one cube sample), Fresnel rim, a soft specular streak from the view direction, subtle noise to hide banding, premultiplied alpha. Stereo-safe macros for single-pass instanced/multiview (the same rule the light-shaft fix needs). No grab pass, no opaque texture, no depth read. Desktop tier may switch on a real blur behind panels (P3-UIX-09).

**Kit:** GlassPanel (9-slice mesh or SDF quad), Header, Button (primary, secondary, danger, icon), Toggle, Slider, Segmented control, List row with state icon, Card, Toast, Dialog, Badge, Progress ring, Tooltip. All built through the `UiKit` factory; one material per style for SRP batching.

**Icons:** Material Symbols (Apache-2.0) or Lucide (ISC) as an SDF/MSDF atlas; licence row added.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-UIX-01 | Tokens: `UiTheme` asset and the tokens page (colours, type, spacing, radii, motion, distances, targets) | Moiz | — | Tokens page reviewed by the owner | Planned |
| P3-UIX-02 | Font: confirm licence (Inter, SIL OFL) and import as TMP SDF with fallback; licence row | Moiz | P3-UIX-01 | Licence recorded; atlas built | Planned |
| P3-UIX-03 | Glass shader (one pass, stereo-safe, probe-based frost, rim, streak), with EditMode shader compile test and a desktop capture | Moiz | P3-UIX-01, P3-PRF-01 | Renders in both eyes in the multiview test scene of the diagnostic APK | Planned |
| P3-UIX-04 | Component kit (list in section 5) as prefabs plus `UiKit` factory methods | Moiz | P3-UIX-03, P3-UIX-02 | Kit sample scene shows every component | Planned |
| P3-UIX-05 | Migrate existing views (tablet, menu, mode chooser, settings tab, results viewer, phone, EMR, debrief, ward and WHO boards, order panel, wait overlay) to the kit without changing their behaviour | Moiz | P3-UIX-04, P3-SES-01 | All existing UI PlayMode tests pass unchanged | Planned |
| P3-UIX-06 | Icon atlas (modes, procedures, people roles, actions, lightbulb, board, mic, settings) | Moiz | P3-UIX-01 | Atlas and licence row | Planned |
| P3-UIX-07 | Feedback components: toast, tick, error shake, listening ring, progress ring | Moiz | P3-UIX-04, P3-ANI-09 | Used by TRN, VOX, RES | Planned |
| P3-UIX-08 | VR readability and comfort rules updated (angular text size, distances, head-locked limits, contrast) and checked by `Readability` in tests | Moiz | p0-vr-interaction-ui-s10 | Every kit component passes the rules | Planned |
| P3-UIX-09 | Desktop-only extra: real background blur behind panels using the opaque texture on desktop only (never on Android) | Moiz | P3-UIX-03 | Off on Android by build check | Planned |
| P3-UIX-10 | Visual review captures of every screen (fixed cameras) and owner approval | Moiz | P3-UIX-05 | Owner approval recorded | Planned |
| P3-UIX-11 | Connection status chip and banners for the networked Quest (D2): chip on the main menu and the wrist (green "Laptop connected · Cloud AI ready", amber "Reconnecting…" or "Cloud AI unavailable · local fallback", red "Laptop not found" with "Play in menu mode"), version-mismatch banner, server settings page with found laptops, keypad and pairing code | Moiz | P3-UIX-04, P3-NET-08 | Every state of `ServiceStatus` has a visual and a one-line reason (PlayMode with a fake server) | Planned |

## 7. Dependencies and execution order

Tokens → font and shader → kit → icons and feedback → migration → captures. Most other plans' screens depend on P3-UIX-04/05.

## 8. Frontend components

The kit itself.

## 9. Backend and API changes

None.

## 10. AI and voice integration

Listening ring and transcript echo components for VOX.

## 11. Unity objects, prefabs, animations and assets

`Assets/UI/Theme/UiTheme.asset`, `Assets/Shaders/UiGlass.shader`, `Assets/Prefabs/UI/Kit/*`, font atlas, icon atlas.

## 12. Performance

One transparent quad per panel layer, one material per style, text in one TMP material; at most 3 panels open at once; panels close when out of view for 10 s; no per-frame layout rebuilds. Overdraw of panels measured in the diagnostic APK.

## 13. Error handling and edge cases

Panel behind geometry (render queue and depth test chosen so panels stay visible but do not draw through walls when far: fade by distance); very bright backgrounds (tint keeps contrast ≥ 4.5:1, checked on captures); left-handed players (wrist UI mirrors); seated players (heights clamp).

## 14. Testing

Shader compile test, kit PlayMode tests (raycast hit areas, sizes vs rules), captures, headset readability in DC-14.

## 15. Acceptance criteria

1. Every screen uses the kit and passes the readability rules.
2. Glass panels render correctly in both eyes on the Quest diagnostic APK and add under 0.5 ms GPU in the measured views.
3. The owner approves the look from captures.

## 16. Definition of Done

All UIX tasks Done; tokens page and licences recorded; headset readability confirmed by Sannan.

# UI design tokens (P3-UIX-01)

The values every ProcSense screen is built from. They live in one asset, `UiTheme` (`unity/Assets/Scripts/UI/Theme/UiTheme.cs`; the shipped values are its defaults, and a `Resources/UiTheme` asset overrides them). The UiKit factory and the glass shader read them, so a colour or a size changes in one place.

`unity/Assets/Tests/EditMode/UiThemeTests.cs` checks them against the readability rules (`docs/plans/p0/p0-vr-interaction-ui/ui-guidelines.md`).

**Status:** written 11 Nov 2026 for the owner's review. The two values that differ from the plan's first table (p3-12, section 5) are explained under "Changed from the plan".

## Colours

| Token | Value | Use |
|---|---|---|
| Glass tint | `#0F1A24` at **85 %** | Every panel |
| Glass tint, light | `#E8F0F6` at 18 % | Light variant (cards on cards) |
| Edge | white at 35 %, 1.5 units | Panel rim, brighter at the top |
| Action | `#2EC4B6` teal | Primary buttons, links, the selected item |
| Hint | `#F2B33D` amber | Hints, the lightbulb, warnings |
| Danger | `#E5484D` red (fill) | Critical buttons and badges |
| Danger as text | **`#FF8589`** | Red text on glass |
| Done | `#3CCB7F` green | Ticks, completed items |
| Text | `#F4F7FA`, with secondary at 72 % and disabled at 45 % | All text on glass |
| Text on an accent fill | `#0F1A24` | Labels on teal, amber, red and green buttons |

## Type

The sizes are canvas units for a panel read from **0.6 m** at **0.0006 m per unit**, the tablet's scale.

| Style | Size | At any distance |
|---|---|---|
| Title | 48 | about 69 headset pixels |
| Heading | 32 | about 46 |
| Body | 24 | about 34 |
| Caption | 18 | about 26 (the minimum is 24) |

A panel further away gets a larger scale: `UiTheme.MetresPerUnit(distance) = 0.0006 × distance ÷ 0.6`. Text therefore looks the same size at every distance, and every style keeps at least 24 headset pixels.

Font: Inter (SIL OFL 1.1, confirmed in P3-UIX-02): `Inter SDF` is the TextMeshPro default, `Inter SemiBold SDF` and `Inter Bold SDF` are its 600 and 700 weights (the <b> tag), LiberationSans SDF the fallback. Built by `Assets/Editor/UI/UiFontBuilder.cs`; provenance in `unity/Assets/UI/Fonts/Inter/README.md`.

Icons: Lucide outline icons (ISC, P3-UIX-06) in one white atlas, `UiIcons.Get(UiIcons.Hint)`; tint with the colour tokens. Built by `task ui-icons`; provenance in `unity/Assets/UI/Icons/README.md`.

## Shape, spacing and targets

| Token | Value |
|---|---|
| Corner radius | 24 units |
| Spacing steps | 8, 16, 24, 32 units |
| Smallest button | 64 × 64 units, 12 units apart. That is at least 2.2° wide, and 3.7° with the distance scale. |

## Motion

| Event | Duration |
|---|---|
| Open | 180 ms, ease-out |
| Close | 120 ms |
| Hover | 60 ms |
| Tick | 250 ms |

With "reduce motion" on (P3-SET-06), everything except feedback ticks becomes instant.

## Distances

| Placement | Distance |
|---|---|
| Menus | 1.2 to 1.6 m |
| Contextual panels | 0.6 to 1.2 m |
| Wrist UI | 0.35 m |
| Nothing closer than | 0.3 m |

## Changed from the plan

1. **Glass at 85 %, not 62 %.** A panel can stand in front of the sunlit window. At 62 %, accent text on the glass fails WCAG AA there: teal reaches 2.3:1, amber 2.7:1 and red 1.3:1, against 4.5:1 needed. At 85 % every text colour passes over white, black, grey and the warm wall colour. The worst case is the lighter red at 4.8:1. The frosted look comes from the probe-sampled sheen and the rim (P3-UIX-03), so it doesn't depend on seeing through the glass.
2. **The canvas scale grows with distance.** At the plan's fixed 0.0006 m per unit, body text on a menu at 1.2 m is about 17 headset pixels, under the 24-pixel minimum. With the distance scale it stays at 34.
3. **Red text is lighter than the red fill.** `#E5484D` stays the fill, with dark text on it (4.5:1). On glass, red text uses `#FF8589`.

All three are checked by `UiThemeTests`. Each check also includes the counter-example, showing the plan's value would fail.

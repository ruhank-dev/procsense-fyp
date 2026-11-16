# UI icons

P3-UIX-06. 34 outline icons from Lucide (https://lucide.dev), lucide-static 0.460.0, ISC licence
(copyright Lucide Contributors, portions Cole Bemis as part of Feather, MIT). The licence text is
`tools/ui/icons/lucide/LICENSE`; the unmodified SVG sources are in `tools/ui/icons/lucide/`,
downloaded 15 November 2026 from the npm package through cdn.jsdelivr.net.

`Resources/UiIcons.png` (1024 x 1024, white on transparent, 128 px cells, 8 per row) and
`Resources/UiIcons.json` (names in cell order) are built by `tools/ui/build_icon_atlas.py`
(`task ui-icons`) with headless Microsoft Edge. `UiIcons.Get(name)` returns the sprite of one
icon; the constants in `Assets/Scripts/UI/Theme/UiIcons.cs` name the icon of each use.

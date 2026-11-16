# Inter (UI font)

P3-UIX-02. Inter by Rasmus Andersson and the Inter Project Authors, licensed under the
SIL Open Font License 1.1 (SIL OFL, `OFL.txt` beside this file). The OFL allows the font to be
bundled and redistributed with the app; the font may not be sold on its own, and a modified
version may not use the name "Inter".

Files: `Inter-Regular.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf` are the unmodified static
instances (weights 400, 600, 700) served by Google Fonts (fonts.gstatic.com, Inter v20),
downloaded 15 November 2026.

The TextMeshPro assets `Assets/TextMesh Pro/Resources/Fonts & Materials/Inter SDF.asset`
(default font), `Inter SemiBold SDF.asset` and `Inter Bold SDF.asset` are built from these files
by `Assets/Editor/UI/UiFontBuilder.cs` (menu ProcSense/UI/Build Inter font assets): SDF, 48 pt
sampling, 6 px padding, static 1024 x 1024 atlas of Latin, Latin-1 and common punctuation;
LiberationSans SDF is the fallback for anything else.

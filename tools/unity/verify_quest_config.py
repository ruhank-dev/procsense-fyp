"""Verify the approved Quest 2 render optimisations in the actual project files (no Unity, no headset).

  uv run --project tools tools/unity/verify_quest_config.py [--head]

Reads ProjectSettings, the Android URP asset and renderer, the URP global settings, the OpenXR package settings, the scenes'
lighting data and reflection probes, the texture import settings and the runtime initialisers, and prints PASS / WARN / FAIL per item
of docs/audit/graphics_matrix.md. --head also prints the committed (git HEAD) value of each settings file item that differs from the
working tree, so a regression introduced by uncommitted work shows up next to the value it replaced. Exit code 1 when any item FAILs
(WARN does not fail: it marks something only a headset or an owner decision can settle). Source-level only: never proof of headset behaviour.
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
U = ROOT / "unity"
P_PROJECT = "unity/ProjectSettings/ProjectSettings.asset"
P_QUALITY = "unity/ProjectSettings/QualitySettings.asset"
P_URP = "unity/Assets/Settings/URP_Quest_Android.asset"
P_RENDERER = "unity/Assets/Settings/URP_Quest_Android_Renderer.asset"
P_GLOBAL = "unity/Assets/UniversalRenderPipelineGlobalSettings.asset"
P_OPENXR = "unity/Assets/XR/Settings/OpenXR Package Settings.asset"
P_LOADERS = "unity/Assets/XR/XRGeneralSettingsPerBuildTarget.asset"
P_LIGHTING = "unity/Assets/Settings/HospitalLighting/PatientBay.lighting"
P_PROFILE = "unity/Assets/Scripts/Views/QuestPerformanceProfile.cs"
P_RUNTIME = "unity/Assets/Scripts/Views/QuestRuntimeSettings.cs"
P_GUARD = "unity/Assets/Scripts/Views/QuestLightingGuard.cs"
P_TIER = "unity/Assets/Scripts/XR/QuestRenderTier.cs"
P_BUILD = "unity/Assets/Editor/Setup/BuildScript.cs"
# Close-up textures of the sharpness pass (15 Nov 2026): Android ASTC 4x4 (48) or 5x5 (49), never the 6x6/8x8 blocks used elsewhere
HERO_TEXTURES = (
    "unity/Assets/Models/Characters/MPFBPatient/Textures/middleage_lightskinned_male_diffuse2.png",
    "unity/Assets/Characters/Rocketbox/Medical_Male_04/m162_body_color_patient.png",
    "unity/Assets/Characters/Rocketbox/Medical_Male_04/m162_head_color.jpg",
    "unity/Assets/Textures/Hospital/FinalLook/gown_albedo.png",
    "unity/Assets/Textures/Hospital/FinalLook/gown_normal.png",
    "unity/Assets/Textures/Hospital/Bed/bed_cover_albedo.png",
    "unity/Assets/Textures/Hospital/Bed/bed_cover_normal.png",
    "unity/Assets/Textures/Hospital/Bed/bed_pillow_normal.png",
    "unity/Assets/Textures/Hospital/Monitor/monitor_ui.png",
    "unity/Assets/Textures/Hospital/Monitor/monitor_labels.png",
)
BAKED_SCENES = ("PatientBay", "Corridor", "OT")
ASTC = set(range(48, 54)) | set(range(66, 72))  # TextureFormat ASTC 4x4..12x12 and ASTC HDR

results: list[tuple[str, str, str]] = []
HEAD = "--head" in sys.argv


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8", errors="replace")


def head(rel: str) -> str | None:
    r = subprocess.run(["git", "show", f"HEAD:{rel}"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout if r.returncode == 0 else None


def val(text: str, key: str) -> str | None:
    m = re.search(rf"^\s*{re.escape(key)}:\s*(.*?)\s*$", text, re.M)
    return m.group(1) if m else None


def block(text: str, name: str) -> str:
    """One YAML document of a multi-object asset, found by its m_Name."""
    for doc in re.split(r"^--- !u!", text, flags=re.M):
        if re.search(rf"^\s*m_Name: {re.escape(name)}\s*$", doc, re.M):
            return doc
    return ""


def check(item: str, ok: bool | None, actual: str, rel: str | None = None, key: str | None = None, name: str | None = None) -> None:
    """Records an item. ok None = WARN. With --head, appends the HEAD value of rel/key (inside block name) when it differs."""
    status = "WARN" if ok is None else ("PASS" if ok else "FAIL")
    if HEAD and rel and key:
        now, old = read(rel), head(rel)
        if old is not None:
            a = val(block(now, name) if name else now, key)
            b = val(block(old, name) if name else old, key)
            if a != b:
                actual += f"  (HEAD: {key}={b})"
    results.append((status, item, actual))


def guid_of(rel: str) -> str | None:
    meta = ROOT / (rel + ".meta")
    return val(meta.read_text(encoding="utf-8"), "guid") if meta.exists() else None


def main() -> int:
    project, quality, urp, renderer, glob, openxr = map(read, (P_PROJECT, P_QUALITY, P_URP, P_RENDERER, P_GLOBAL, P_OPENXR))

    # profile binding
    medium = next((b for b in quality.split("  - serializedVersion:") if re.search(r"^\s*name: Medium\s*$", b, re.M)), "")
    android_default = re.search(r"m_PerPlatformDefaultQuality:(?:\n\s+\w+: \d+)*?\n\s+Android: (\d+)", quality)
    medium_guid = re.search(r"customRenderPipeline: \{fileID: \d+, guid: (\w+)", medium)
    others_exclude = all("- Android" in b for b in quality.split("  - serializedVersion:")[1:] if not re.search(r"^\s*name: Medium\s*$", b, re.M))
    check("Android quality -> URP_Quest_Android", bool(android_default and android_default.group(1) == "2" and medium_guid and medium_guid.group(1) == guid_of(P_URP) and others_exclude),
          f"Android default {android_default.group(1) if android_default else '?'}, Medium pipeline {'= URP_Quest_Android' if medium_guid and medium_guid.group(1) == guid_of(P_URP) else '!= URP_Quest_Android'}, other levels exclude Android: {others_exclude}")

    # graphics API and player
    api = re.search(r"m_BuildTarget: AndroidPlayer\s*\n\s*m_APIs: (\w+)\s*\n\s*m_Automatic: (\d)", project)
    check("Vulkan only (no GLES fallback, auto off)", bool(api and api.group(1) == "15000000" and api.group(2) == "0"), f"m_APIs {api.group(1) if api else '?'}, m_Automatic {api.group(2) if api else '?'}")
    backend = re.search(r"scriptingBackend:\s*\n(?:\s+\w+: \d+\n)*?\s+Android: (\d)", project)
    strip = re.search(r"managedStrippingLevel:\s*\n(?:\s+\w+: \d+\n)*?\s+Android: (\d)", project)
    check("IL2CPP ARM64, engine code stripped, managed stripping >= Medium",
          bool(backend and backend.group(1) == "1" and val(project, "AndroidTargetArchitectures") == "2" and val(project, "stripEngineCode") == "1" and strip and int(strip.group(1)) >= 2),
          f"backend {backend.group(1) if backend else '?'}, arch {val(project, 'AndroidTargetArchitectures')}, stripEngineCode {val(project, 'stripEngineCode')}, managed {strip.group(1) if strip else '?'}", P_PROJECT, "stripEngineCode")

    # OpenXR
    loaders = block(read(P_LOADERS), "Android Providers")
    check("OpenXR loader on Android", guid_of("unity/Assets/XR/Loaders/OpenXRLoader.asset") in loaders, "Android Providers lists OpenXRLoader" if guid_of("unity/Assets/XR/Loaders/OpenXRLoader.asset") in loaders else "OpenXRLoader missing")
    android = block(openxr, "Android")
    check("OpenXR multiview (Single Pass Instanced)", val(android, "m_renderMode") == "1", f"Android m_renderMode {val(android, 'm_renderMode')}", P_OPENXR, "m_renderMode", "Android")
    meta = block(openxr, "MetaQuestFeature Android")
    devices = dict(re.findall(r"manifestName: (\w+)\s*\n\s*enabled: (\d)", meta))
    check("Meta Quest feature on; Quest 2 on, Quest 1 off", val(meta, "m_enabled") == "1" and devices.get("quest2") == "1" and devices.get("quest") == "0", f"feature {val(meta, 'm_enabled')}, devices {devices}")
    for key, label in (("m_symmetricProjection", "symmetric projection"), ("m_optimizeBufferDiscards", "optimize buffer discards"), ("optimizeMultiviewRenderRegions", "multiview render regions")):
        check(label, val(meta, key) == "1", f"MetaQuestFeature Android {key} {val(meta, key)}", P_OPENXR, key, "MetaQuestFeature Android")
    fov = block(openxr, "FoveatedRenderingFeature Android")
    profile = read(P_PROFILE)
    fov_level = re.search(r"const float Foveation = ([\d.]+)f", profile)
    check("FFR on (Medium), subsampled layout", val(fov, "m_enabled") == "1" and val(fov, "enableSubsampledLayout") == "1" and fov_level is not None and 0.375 <= float(fov_level.group(1)) < 0.75,
          f"feature {val(fov, 'm_enabled')}, subsampled {val(fov, 'enableSubsampledLayout')}, level {fov_level.group(1) if fov_level else '?'}", P_OPENXR, "enableSubsampledLayout", "FoveatedRenderingFeature Android")
    perf = block(openxr, "XrPerformanceSettingsFeature Android")
    runtime = read(P_RUNTIME)
    check("XR performance settings (SustainedHigh hint)", val(perf, "m_enabled") == "1" and "PerformanceLevelHint.SustainedHigh" in runtime and "RuntimeInitializeOnLoadMethod" in runtime,
          f"XrPerformanceSettingsFeature Android m_enabled {val(perf, 'm_enabled')}, QuestRuntimeSettings hint {'SustainedHigh' if 'SustainedHigh' in runtime else '?'}", P_OPENXR, "m_enabled", "XrPerformanceSettingsFeature Android")
    sw, adr = block(openxr, "SpaceWarpFeature Android"), block(openxr, "AutomaticDynamicResolutionFeature Android")
    check("AppSW and OpenXR auto dynamic resolution off (documented)", val(sw, "m_enabled") == "0" and val(adr, "m_enabled") == "0", f"SpaceWarp {val(sw, 'm_enabled')}, AutomaticDynamicResolution {val(adr, 'm_enabled')}")

    # URP asset and renderer
    check("MSAA 1 (Android URP; Medium quality AA 0)", val(urp, "m_MSAA") == "1" and val(medium, "antiAliasing") == "0", f"m_MSAA {val(urp, 'm_MSAA')}, Medium antiAliasing {val(medium, 'antiAliasing')}", P_URP, "m_MSAA")
    check("HDR off", val(urp, "m_SupportsHDR") == "0", f"m_SupportsHDR {val(urp, 'm_SupportsHDR')}", P_URP, "m_SupportsHDR")
    check("no depth / opaque texture", val(urp, "m_RequireDepthTexture") == "0" and val(urp, "m_RequireOpaqueTexture") == "0", f"depth {val(urp, 'm_RequireDepthTexture')}, opaque {val(urp, 'm_RequireOpaqueTexture')}", P_URP, "m_RequireDepthTexture")
    check("no post-processing / motion blur, no renderer features", val(renderer, "postProcessData") == "{fileID: 0}" and val(renderer, "m_RendererFeatures") == "[]",
          f"postProcessData {val(renderer, 'postProcessData')}, features {val(renderer, 'm_RendererFeatures')}", P_RENDERER, "postProcessData")
    check("forward, depth priming off, intermediate texture Auto", val(renderer, "m_RenderingMode") == "0" and val(renderer, "m_DepthPrimingMode") == "0" and val(renderer, "m_IntermediateTextureMode") == "0",
          f"mode {val(renderer, 'm_RenderingMode')}, priming {val(renderer, 'm_DepthPrimingMode')}, intermediate {val(renderer, 'm_IntermediateTextureMode')}")
    check("realtime shadows off (main + additional)", val(urp, "m_MainLightShadowsSupported") == "0" and val(urp, "m_AdditionalLightShadowsSupported") == "0",
          f"main {val(urp, 'm_MainLightShadowsSupported')}, additional {val(urp, 'm_AdditionalLightShadowsSupported')}", P_URP, "m_MainLightShadowsSupported")
    check("SRP batcher on, dynamic batching off", val(urp, "m_UseSRPBatcher") == "1" and val(urp, "m_SupportsDynamicBatching") == "0", f"SRP batcher {val(urp, 'm_UseSRPBatcher')}, dynamic {val(urp, 'm_SupportsDynamicBatching')}")
    check("URP render scale 1 (scale applied at run time)", val(urp, "m_RenderScale") == "1", f"m_RenderScale {val(urp, 'm_RenderScale')}", P_URP, "m_RenderScale")
    check("LOD cross-fade off", val(urp, "m_EnableLODCrossFade") == "0", f"m_EnableLODCrossFade {val(urp, 'm_EnableLODCrossFade')}", P_URP, "m_EnableLODCrossFade")
    strips = re.findall(r"m_StripUnused(?:PostProcessing)?Variants: (\d)", glob)
    check("shader stripping (unused + post-processing + debug variants)", bool(strips) and set(strips) == {"1"} and val(glob, "m_StripDebugVariants") == "1",
          f"strip flags {strips}, debug {val(glob, 'm_StripDebugVariants')}", P_GLOBAL, "m_StripUnusedPostProcessingVariants")

    # lighting: baked, non-directional, shared LightingData, probes
    lighting = read(P_LIGHTING)
    maps_dir = U / "Assets/Scenes/PatientBay"
    maps = sorted(maps_dir.glob("Lightmap-*_comp_light.exr"))
    dirs = list(maps_dir.glob("Lightmap-*_comp_dir.*")) + list(maps_dir.glob("Lightmap-*_comp_shadowmask.*"))
    ld_guid = guid_of("unity/Assets/Scenes/PatientBay/LightingData.asset")
    scene_text = {s: read(f"unity/Assets/Scenes/{s}.unity") for s in BAKED_SCENES}
    shared = all(re.search(rf"m_LightingDataAsset: \{{fileID: \d+, guid: {ld_guid}", t) for t in scene_text.values())
    realtime_gi = [s for s, t in scene_text.items() if val(t, "m_EnableRealtimeLightmaps") != "0" or val(t, "m_EnableBakedLightmaps") != "1"]
    check("baked non-directional lightmaps present, shared LightingData",
          val(lighting, "m_LightmapsBakeMode") == "0" and len(maps) >= 1 and not dirs and shared and not realtime_gi and all(m.stat().st_size > 0 and Path(str(m) + ".meta").exists() for m in maps),
          f"bake mode {val(lighting, 'm_LightmapsBakeMode')} (0 = non-directional), {len(maps)} lightmaps, {len(dirs)} dir/shadowmask maps, 3 scenes share LightingData: {shared}, scenes with realtime GI or no baked maps: {realtime_gi or 'none'}",
          P_LIGHTING, "m_LightmapsBakeMode")
    lights = {s: re.findall(r"^Light:\n(?:(?!^\S).*\n)*?\s+m_Lightmapping: (\d)", t, re.M) for s, t in scene_text.items()}
    not_baked = {s: sum(1 for x in v if x != "2") for s, v in lights.items()}
    check("all lights baked in the baked rooms", all(v == 0 for v in not_baked.values()) and all(lights.values()), f"lights per scene {({s: len(v) for s, v in lights.items()})}, not baked {not_baked}")

    guids: dict[str, Path] = {}
    probe_issues, baked_enabled = [], 0
    for s, t in scene_text.items():
        for doc in re.split(r"^--- !u!", t, flags=re.M):
            if not doc.lstrip().startswith("215 ") and "\nReflectionProbe:\n" not in doc:
                continue
            enabled, mode = val(doc, "m_Enabled"), val(doc, "m_Mode")
            if mode == "1":
                probe_issues.append(f"{s}: realtime probe")
            if enabled != "1":
                continue
            if mode == "0":
                baked_enabled += 1
            elif mode == "2":
                g = re.search(r"m_CustomBakedTexture: \{fileID: \d+, guid: (\w+)", doc or "")
                if not guids:
                    for m in (U / "Assets").rglob("*.meta"):
                        gg = re.search(r"^guid: (\w+)", m.read_text(encoding="utf-8", errors="replace"), re.M)
                        if gg:
                            guids[gg.group(1)] = m.with_suffix("")
                if not g or g.group(1) not in guids or not guids[g.group(1)].exists():
                    probe_issues.append(f"{s}: custom probe texture missing")
    probe_files = [p for p in maps_dir.glob("ReflectionProbe-*.exr") if p.stat().st_size > 0 and Path(str(p) + ".meta").exists()]
    check("reflection probes valid (baked/custom, none realtime, Medium realtimeReflectionProbes 0)",
          not probe_issues and len(probe_files) >= baked_enabled and val(medium, "realtimeReflectionProbes") == "0",
          f"{baked_enabled} enabled baked probes, {len(probe_files)} baked probe files, issues: {probe_issues or 'none'} (pixel values are checked in the Editor, not here)")

    # textures
    overrides, non_astc, files = 0, [], 0
    for meta in (U / "Assets").rglob("*.meta"):
        if not meta.name.lower().endswith((".png.meta", ".jpg.meta", ".jpeg.meta", ".tga.meta", ".psd.meta", ".tif.meta", ".tiff.meta", ".exr.meta")):
            continue
        t = meta.read_text(encoding="utf-8", errors="replace")
        if "TextureImporter:" not in t:
            continue
        files += 1
        m = re.search(r"buildTarget: Android\s*\n(?:\s+\w+:.*\n)*?\s+textureFormat: (-?\d+)(?:\s*\n\s+\w+:.*)*?\n\s+overridden: (\d)", t)
        if m and m.group(2) == "1":
            overrides += 1
            if int(m.group(1)) not in ASTC and int(m.group(1)) != -1:
                non_astc.append(f"{meta.relative_to(U)} ({m.group(1)})")
    check("ASTC overrides on Android", overrides >= 400 and len(non_astc) <= 20,
          f"{overrides} of {files} textures override Android; {len(non_astc)} use a non-ASTC format" + (f" e.g. {non_astc[:3]}" if non_astc else ""))
    hero_bad = []
    for rel in HERO_TEXTURES:
        t = read(rel + ".meta")
        m = re.search(r"buildTarget: Android\s*\n(?:\s+\w+:.*\n)*?\s+textureFormat: (-?\d+)(?:\s*\n\s+\w+:.*)*?\n\s+overridden: (\d)", t)
        if not m or m.group(2) != "1" or m.group(1) not in ("48", "49"):
            hero_bad.append(f"{rel.rsplit('/', 1)[-1]} ({m.group(1) if m else '?'})")
    check("close-up textures ASTC 4x4/5x5 (patient, gown, bed, monitor)", not hero_bad, f"{len(HERO_TEXTURES) - len(hero_bad)} of {len(HERO_TEXTURES)} OK" + (f"; not 4x4/5x5: {hero_bad}" if hero_bad else ""))
    check("project default Android texture compression explicit", None if val(project, "m_BuildTargetDefaultTextureCompressionFormat") == "[]" else True,
          f"m_BuildTargetDefaultTextureCompressionFormat {val(project, 'm_BuildTargetDefaultTextureCompressionFormat')} (unset: Unity 6 defaults to ASTC; matrix action, not a regression)")

    # runtime profile
    num = lambda n: (re.search(rf"const float {n} = ([\w.]+?)f?;", profile) or [None, None])[1]
    dyn = re.search(r"public static bool DynamicResolution = (\w+);", profile)
    check("eye buffer 1.0 / start 0.9 / dynamic 0.85-1.0, driver installs itself",
          num("EyeBufferScale") == "1" and num("StartScale") == "0.9" and num("MinScale") == "0.85" and "MaxScale = EyeBufferScale" in profile and dyn and dyn.group(1) == "true"
          and "RuntimeInitializeOnLoadMethod" in profile and "QuestRenderTier.SetEyeScale(EyeBufferScale)" in profile,
          f"eye {num('EyeBufferScale')}, start {num('StartScale')}, min {num('MinScale')}, dynamic {dyn.group(1) if dyn else '?'}")
    build = read(P_BUILD)
    timing_all = re.search(r"^\s*PlayerSettings\.enableFrameTimingStats = true;", build, re.M) is not None and "if (v.Diagnostic) PlayerSettings.enableFrameTimingStats" not in build
    timing_restored = "PlayerSettings.enableFrameTimingStats = timingBefore;" in build
    check("dynamic resolution GPU time in release and diagnostic APKs", True if val(project, "enableFrameTimingStats") == "1" or (timing_all and timing_restored) else None,
          f"enableFrameTimingStats {val(project, 'enableFrameTimingStats')} in the project; BuildScript sets it for every Android build: {timing_all}, restores it after: {timing_restored} (DT-306; whether FrameTimingManager reports GPU time on Quest Vulkan is headset DC-13)")
    guard, tier = read(P_GUARD), read(P_TIER)
    check("single source of the run-time scale and foveation", "DefaultFoveation = ProcSense.VR.Views.QuestPerformanceProfile.Foveation" in tier and "DefaultEyeScale = ProcSense.VR.Views.QuestPerformanceProfile.EyeBufferScale" in tier and val(urp, "m_RenderScale") == "1",
          "QuestRenderTier defaults are the profile constants, URP render scale 1 (no double reduction)")
    check("lighting guard and render tier initialisers present", "RuntimeInitializeOnLoadMethod" in guard and "ShadowCastingMode.Off" in guard and "RuntimeInitializeOnLoadMethod" in tier,
          "QuestLightingGuard (shadow casting off on Android), QuestRenderTier (foveation) install at scene load")

    width = max(len(i) for _, i, _ in results)
    for status, item, actual in results:
        print(f"{status:4}  {item:<{width}}  {actual}")
    fails = sum(1 for s, _, _ in results if s == "FAIL")
    warns = sum(1 for s, _, _ in results if s == "WARN")
    print(f"\n{len(results) - fails - warns} PASS, {warns} WARN, {fails} FAIL (source-level; headset effect not verified)")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())

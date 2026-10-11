# Procedural equipment pilots

Requires **Blender 4.5.14 LTS**, checked exactly by `run.py`; Python tooling uses the existing `uv` environment and no new dependencies. Set `BLENDER_EXE` to the executable, put it on PATH, or extract the portable Windows build into `build/blender/blender-4.5.14-windows-x64/`.

Official portable archive: [Blender 4.5 downloads](https://download.blender.org/release/Blender4.5/). The `blender-4.5.14-windows-x64.zip` SHA-256 is `b9533d2397ac1984db4466fb23a7a4649391cca93f6e84209f9bcc60d071c8b9`; verify against the accompanying official checksum file before extracting. Blender is GPL licensed; generated original equipment geometry is project output, not downloaded third-party models.

From the repository root:

```powershell
task blender-doctor
task blender-build-all
task blender-selftest
# Individual operations:
task blender-build -- vital_signs_monitor
task blender-validate -- all
task blender-preview -- wristband
task blender-build -- hello_cube
```

The runner uses background factory startup, propagates failures, and rejects other Blender versions. Build validates before exporting. `build/blender/` holds `.blend`, FBX, metadata and validation JSON; `build/previews/` holds 1024px front, side, top, three-quarter and human-scale views. Both are ignored. Use **ProcSense → Equipment → Import Pilot Assets** in the existing Unity Editor to copy validated exports and create materials/prefabs through AssetDatabase. Only imported FBX, metadata and Unity assets are committed; Blender source files remain reproducible outputs. Never copy `.blend` files into Unity.

## Reference and provisional quality bar

The body-only dimensions come from the existing equipment sheets: [monitor](../../docs/research/equipment/09-vital-sign-monitor.md), [pump](../../docs/research/equipment/27-infusion-pump.md), [wristband](../../docs/research/equipment/01-patient-wristband.md). Axes in Blender are width/depth/height; Unity is width/height/depth.

| Asset | Sourced body dimensions | Current triangles | Initial budget |
|---|---|---:|---:|
| Monitor | 327 × 182 × 288 mm | 3,768 | 20,000 |
| Pump | 214 × 124 × 68 mm | 2,452 including pole | 20,000 |
| Wristband | 279 × 25 × 1 mm | 324 | 5,000 |

Wristband length/width are sourced representative adult stock; its **1 mm thickness is provisional**, as are stand/pole heights, castors, basket, hook reach and worn fit. The band is deliberately flat, with an adhesive closure and blank identity surface; it is not an accepted patient-wrist fit. The validator's thickness check is a modelling consistency check, not a sourced medical dimension claim.

Manufactured edges use two-segment bevels; separate buttons, doors, ports and handles retain names. Meshes have UVs, applied rotation/scale, closed manifold geometry, `EQ_<asset>_<part>` names and shared `M_*` materials. Validation checks named anchors, body dimensions within 5%, triangle and material budgets. It does not establish clinical usability, assembly interference, reach or headset readability. The 1.7 m mannequin and 1 m scale bar are visual references.

Current pilots are already below budget without LOD reduction; a production LOD policy, curved cable helper, final label layout, door animation, fitted wristband and material-vs-baked-texture Quest comparison remain follow-ups. Do not mass-produce the roster until the pilot appearance and headset quality bar are accepted.

## Unity behaviour and review

**ProcSense → Equipment → Open Pilot Review** opens the independent review scene, prompting to save modified scenes. Inspect the monitor face, pump hooks and wristband on the plinth. The existing PatientBay is untouched. To exercise XR interaction, drag a pilot prefab into a temporary bay instance with the current rig/services and enter Play; discard that placement after review.

The import pipeline creates shared URP materials, compound box colliders and grabbable prefabs. Bag hook sockets use interaction layer bit 8; normal layer-0 props must not snap into them. This bit is reserved locally for future compatible bag props. `SOCKET_Wrist` is an alignment marker, **not a functional wrist socket**: patient attachment/fit is not yet implemented. Cable points and door pivot remain named attachment markers.

Monitor/identity surfaces render permitted observable state; no patient facts are baked in. Grabbing the wristband submits the existing `read_wristband` action. Monitor grabbing only moves equipment; it never attaches a sensor or invents values. Pump reads STANDBY and has no dose/rate action. Bag hooks have no clinical effect. Screen controls and physical sensor attachment belong to the next bedside batch.

Nested model instances refresh on FBX reimport. Existing prefab component settings and material assets are preserved; the builder only creates missing prefabs. Changes to the interaction schema require an explicit migration; reimport intentionally does not overwrite authored settings. Import tests check dimensions, materials, socket isolation and preservation of an edited mass/GUID. Run Unity tests in the live Test Runner while the Editor is open.

# CGTrader patient: local static trial

Source supplied by Sannan: https://www.cgtrader.com/items/6984099/download-page
Original downloads remain intact in repository-root patientModel/. Public product page and licence are unverified; Sannan explicitly authorized local use on 13 October 2026. This inspection does not establish redistribution rights. Sannan authorized a local baseline commit on 13 October 2026; no upload or push is authorized.

Original: one 1,500,000-triangle mesh, no armature, animations or facial shape keys, three 4096px textures. Standing male wearing blue scrubs, not a hospital gown. Original height is about 1.18 model units; the trial uses a provisional 1.75m height, not a sourced anatomical measurement.

Trial: welded seam vertices, corrected face winding, reduced to 40,000 triangles, new UV atlas and 2048px diffuse bake. Unity uses URP Lit, calculated normals, mipmaps and Android ASTC 6x6. Original normal map is not used after topology changes. Body is statically rotated face-up in PatientBay; no clinical signs, breathing or speech animation is implied. Existing greybox renderers are hidden while their interaction/collision components remain. Cuff/probe socket fitting to this body remains a separate review item.

Reproduce from repository root with the pinned Blender executable and --factory-startup --python-exit-code 1 -P unity/Tools/prepare_patient_trial.py. It reads patientModel/3d-410.blend and writes generated FBX/texture to ignored unity/Logs/patient-review/. Import through the connected Editor/AssetDatabase, preserving current GUIDs. The source file was written by a newer Blender version; 4.5.14 loads it with compatibility warnings. Originals are never saved over.

## Mid-evaluation equipment demo

The patient retains the original patient_review.fbx pose. An attempted coordinate-based arm adjustment stretched the skin and clothing together; it was rejected during Sannan review and removed from Assets. No arm straightening or rigging is included in the current demo. Fingers remain curled as supplied.

make_patient_wristband.py builds the annulus in Blender and exports patient_wristband.obj into ignored unity/Logs/patient-review. Run it from repository root with pinned Blender and --factory-startup --python-exit-code 1. Import/reimport the OBJ through AssetDatabase preserving metadata. Open and save PatientBay, then run Tools/FitPatientDemo.cs through the labelled, project-targeted Unity CLI run_script command. The fitter preserves the original patient mesh and adjusts only sockets, sensor visuals and wristband placement. It rejects Play Mode and unsaved scenes and reuses the existing band.

The fitted cuff/probe retain SensorAttachment and Core acceptance. The fixed wristband uses XRSimpleInteractable/XRSelectEmitter and read_wristband; EquipmentSurfaceView displays identifiers only after acceptance and clears on reset. The old wristband proxy is disabled and preserved.

This remains a rough static scan. Source surface/texture artifacts, curled fingers and approximate sensor fit remain; it is not an anatomically validated animated patient. Headset readability/comfort and full clinical presentation remain pending. MPFB replacement is planned after evaluation.
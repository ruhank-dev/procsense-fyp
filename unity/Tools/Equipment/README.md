# Padded cuff and dropped equipment

Run Blender 4.5.14 with factory startup:

```powershell
& './build/blender/blender-4.5.14-windows-x64/blender.exe' -b --factory-startup --python-exit-code 1 -P unity/Tools/Equipment/build_padded_cuff.py
```

Outputs go to ignored `unity/Logs/CuffBuild`. The script reads the shared Blender
helpers and the original cuff's wrapped-state geometry without editing either.
It replaces the loose sheet with padded, curved fabric, bound edges, a fastening
patch, retaining loop, index mark and hose connector. Presentation dimensions
are illustrative; this is not a cloth or pressure simulation. The generated
model has 3,160 triangles, manifold geometry and UVs. Original fit/grab anchors
and the wrapped mesh are retained; the additional loose hose port follows the
visible connector while the fitted hose uses its original port.

With the saved PatientBay open in Edit mode, run
**ProcSense > Equipment > Enable Gravity and Update Cuff**. The guarded importer
retains the existing FBX/prefab GUIDs, checks loose-state references and updates
equipment Rigidbody settings. Use this generator for the revised loose cuff;
the older shared pilot generator remains a historical source for its wrapped
mesh and would regenerate the old flat loose model.

Loose equipment uses gravity and speculative continuous collision detection.
XRI temporarily makes a held/socketed item kinematic and restores dynamic
physics on release. Throw impulses remain disabled for predictable drops.
Sensor tray returns clear old linear/angular velocity before reposing, then
settle physically on the tray. They do not emit clinical actions.

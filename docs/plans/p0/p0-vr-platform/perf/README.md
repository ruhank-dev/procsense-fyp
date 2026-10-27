# Performance captures (OVR Metrics)

The weekly capture of p0-vr-platform-s07: the same route on the Quest every time, so a week's numbers can be compared with the last. The gate (T-Q1, T-Q4): average at least 72 fps, no frame over 50 ms (no hitch), memory at most 2.5 GB.

## Set-up

- The Demo build (`task unity-build -- demo`), installed with `adb install -r artifacts/unity/ProcSense-demo.apk`.
- OVR Metrics Tool on the headset, recording to CSV, with the frame time, FPS, memory and CPU/GPU level columns.
- For the detail of a slow frame, the Dev build with the Unity Profiler attached: the markers `ProcSense.Core.Tick`, `ProcSense.Core.Apply` and `ProcSense.Views.Render` show Core's step, the actions and the drawing of the state views.

## The fixed route (about 5 minutes)

1. Boot to the main menu and start VAR-S1-A in practice.
2. In the bay: introduce yourself, ask the identity, read the wristband, open the folder and turn its pages.
3. Open the tablet, go through every tab once, open the EMR and two of its tabs.
4. Order a scan and request transport to it; wait while the patient is away (the waiting overlay); let the patient come back.
5. Walk to the theatre and do the Sign In on the WHO board.
6. End the session and open the debrief.

## Files

Name each CSV `YYYY-MM-DD-<variant>-<commit>.csv` in this folder and add a row:

| Date | Build | Commit | Avg fps | Worst frame (ms) | Peak memory (GB) | Pass | By |
|---|---|---|---|---|---|---|---|
| (none yet) | | | | | | | |

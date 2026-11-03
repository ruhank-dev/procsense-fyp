# 3 Nov 2026: corridor sunshafts, dust and door glass

- Sun 11 deg pitch, yaw 138, intensity 1.9; post exposure -0.8 EV. Plaster base (.80,.765,.70) so the sunlit wall patch keeps texture (pushed warmer than neutral: the NeutralDaylightContractTests were not re-run after this change).
- Desktop-only atmosphere: `ProcSense/Sun Shaft` (additive, noise stretched along the sun, no depth texture needed) with 6 overlapping prisms per window, and `ProcSense/Sun Dust Mote` particles (about 150 per strong shaft, emitted inside the shaft prisms only). Host is DesktopOnly, never drawn on Quest. Rebuild with `CorridorSun.RebuildShafts` (no bake needed).
- Rooms behind the door glass use `Sunlit room interior.mat` (dark) and a 0.22 light so the door glass no longer clips.
- Video capture: `unity/Tools/Rooms/room_video.ps1` (fixed 1/12 s step, ffmpeg). Views JSON path is relative to the unity folder.
- ChatGPT round 25 and 26: REJECTED (broad pale wash, dust visible in shade, weak contact shadows under wheelchair and standing man, ceiling hot spots, sparkling metal). Round 26 predates the last glass/plaster/dust change; not re-reviewed.
- Not done: Quest measurement, head-movement/VR check, final-quality bake, task ci.

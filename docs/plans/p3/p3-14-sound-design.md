# p3-14-sound-design — Complete sound design, ready to implement later (SND)

Status: Planned (P3, 7 November 2026). Nothing in this plan is implemented. The owner said sound is implemented later; this plan is complete enough that no further planning phase is needed.

## 1. Objective and final user-facing behaviour

The menu has a quiet, hopeful music theme. In the hospital, music stops and the player hears the place: ward room tone with distant voices and trolleys, the emergency department busier, the corridor's footsteps and doors, the theatre's air handling and equipment hum. Equipment sounds come from where the equipment is: the monitor's beeps and alarms, the cuff pump, the thermometer's beep. People's footsteps and clothing are heard as they walk in. The glass interface answers softly: a gentle tick on hover, a clear click on select, a glassy swell on open, a short chime on a completed step, a warm "ding" on a hint, a low tone on a refused action, a listening "blip" when the microphone opens. The results arrive with a short sting that differs for passed and not passed. Voices are always intelligible: everything else ducks under speech.

## 2. Current implementation status

`ToneBank` (alarm bursts, cuff pump, lab phone, noise beds synthesised in code), `RoomAudio` (room bed follows the trainee's location, phone ring), `AlarmAudioView`, `VoicePlayer` (pooled sources, subtitle queue), `AudioCatalog`; production voice clips are not in the repository (subtitle fallback); no mixer, no music, no UI sounds, no footsteps or foley; p0-voice-audio-s05 (ambience and alarms) open. **Partial.**

## 3. Existing code, assets, scenes and systems to reuse

`ToneBank`, `RoomAudio`, `AlarmAudioView`, `VoicePlayer`, `AudioCatalog`, `SubtitleView`, `content/voice/`, Piper TTS on the server (`server/tts`), the voice render tools (`tools/ci/test_voice_render.py` covers them).

## 4. Missing functionality

Mixer and snapshots; asset list and sources; ambience recordings; music; UI set; foley; equipment set; voice-feedback cues; spatial settings; trigger wiring; loudness pass.

## 5. Architecture and integrations

- **Mixer `ProcSense.mixer`:** groups Master → {Voice, Music, SFX → {UI, Equipment, Foley}, Ambience}; snapshots Menu, Play, Paused, Results, Listening (ducks ambience -8 dB while the microphone is open); side-chain ducking of Ambience and SFX by Voice (-6 dB).
- **Triggers:** an `AudioDirector` subscribes to `SimFeedback` (P3-SES-10), UI kit events and flow states; no view plays sounds directly, so every sound has one trigger and one volume group.
- **Spatial:** voices, equipment and footsteps are 3D (logarithmic roll-off, min 0.5 m, max 12 m), ambience is 2D per room with a crossfade at doors (`RoomAudio`), UI is 2D quiet. Spatialiser: Unity's built-in HRTF-less panning first; Meta XR Audio SDK spatialiser evaluated in P3-SND-09 (free, Meta licence) only if the headset test asks for it.
- **Formats and budgets:** UI and short SFX as ADPCM, decompressed on load; ambience and music Vorbis, streaming; at most 24 real voices; 48 kHz source, imported at 44.1/48 kHz mono for 3D.
- **Sources (free; licence recorded per file):** Kenney UI Audio and Interface Sounds (CC0), Sonniss GDC game audio bundles (royalty-free licence, check terms per bundle), Freesound items filtered to CC0, the existing `ToneBank` synthesis for monitor tones, Piper/pre-rendered voices, music from CC0 or CC-BY libraries (attribution kept in `docs/ATTRIBUTIONS.md`), own recordings.

**Sound list (implementation-ready):**

| Group | Sounds (trigger) |
|---|---|
| Music | menu theme loop (MainMenu state), results sting pass / not pass / not completed (Results) |
| Ambience | ward bed, ED bed, corridor bed, OT bed, menu room tone (room entry, crossfade 1.5 s) |
| UI | hover, select, back, open glass, close glass, toggle, slider tick, toast, error, confirm, submit |
| Training | step complete chime, all-steps-in-stage chime, board open/close |
| Examination | hint offer, hint reveal, submit confirm, examination end |
| Voice | talk start blip, talk end blip, processing loop (soft), misunderstood tone |
| Equipment | monitor beats and alarms (existing synthesis), cuff inflate/deflate, thermometer beep, glucometer beep, ECG print, phone ring (existing), IV pump alarm, trolley wheels, bed motor, curtain pull, door open/close |
| Foley | footsteps (vinyl, tile) for staff and porter, clothing rustle (nurse coat, scrubs), clipboard, paper, pen signing, wristband snap |
| Transitions | fade whoosh (very soft), loading loop, scene arrive |

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-SND-01 | Mixer asset with groups, snapshots and ducking; `AudioDirector` skeleton | Moiz | — | Snapshots switch with flow states in PlayMode | Planned |
| P3-SND-02 | Asset sourcing: every sound of the list chosen, downloaded, licence recorded (`docs/research/asset_sources_and_licenses.md`, `docs/ATTRIBUTIONS.md`), manifest source record | Sannan | — | List complete with licences | Planned |
| P3-SND-03 | Ambience beds per room and the crossfade through `RoomAudio` | Sannan | P3-SND-01, P3-SND-02, p0-voice-audio-s05 | Crossfade at every door | Planned |
| P3-SND-04 | Menu music and result stings | Moiz | P3-SND-01, P3-SND-02 | Music never plays during a session | Planned |
| P3-SND-05 | UI, training and examination sounds wired to kit and feedback events | Moiz | P3-SND-01, P3-SND-02, P3-SES-10 | One sound per event (test counts plays) | Planned |
| P3-SND-06 | Foley: footsteps by surface, clothing, paper and pen, band snap | Sannan | P3-SND-02, P3-ANI-07 | Footsteps match walk animations | Planned |
| P3-SND-07 | Equipment sounds beyond the existing synthesis | Sannan | P3-SND-02, P3-ANI-08 | Each equipment animation has its sound | Planned |
| P3-SND-08 | Voice-feedback cues and the spoken failure phrases (pre-rendered with the project's voice tools) | Moiz | P3-VOX-07, P3-SND-01 | Each phrase has a clip and subtitle | Planned |
| P3-SND-09 | Spatial settings, voice limit, compression settings; evaluate the Meta XR Audio spatialiser only if DC-14 reports poor localisation | Moiz | P3-SND-03 | Settings recorded; CPU audio time measured in DC-13 | Planned |
| P3-SND-10 | Volume settings integration (P3-SET-03) and the Listening snapshot | Moiz | P3-SET-03, P3-VOX-09 | Ambience ducks while talking | Planned |
| P3-SND-11 | Mix and loudness pass (dialogue around -18 LUFS-S short term in the headset, UI clearly below voice) and tests | Sannan | P3-SND-05, P3-SND-06, P3-SND-07 | Measured levels recorded; tests green | Planned |

## 7. Dependencies and execution order

Mixer and sourcing first; then ambience, UI, music; foley and equipment follow the animations; mix last.

## 8. Frontend components

Volume sliders (P3-SET), subtitle toggles (existing).

## 9. Backend and API changes

None (Piper TTS exists for live lines).

## 10. AI and voice integration

Voice lines play through `VoicePlayer` on the Voice group; failure phrases come from P3-VOX-07.

## 11. Unity objects, prefabs, animations and assets

`Assets/Audio/` (music, ambience, ui, foley, equipment), `ProcSense.mixer`, `AudioDirector` in Sys_Core, audio sources on characters and equipment (pooled).

## 12. Performance

Streaming for long files, at most 24 voices, no realtime reverb on Quest (baked "room tone" instead), audio CPU measured in DC-13.

## 13. Error handling and edge cases

Missing clip (silent and logged, never an exception); many events in one frame (rate limit 1 per sound per 100 ms); headset removed (Paused snapshot).

## 14. Testing

EditMode for the director mappings; PlayMode for snapshot switches and play counts; headset listening checks in DC-14.

## 15. Acceptance criteria

1. Every row of the sound list plays from its trigger and nowhere else.
2. Voices remain intelligible over everything (ducking measured).
3. Every sound file has a recorded licence.

## 16. Definition of Done

All SND tasks Done, licences complete, headset listening check passed.

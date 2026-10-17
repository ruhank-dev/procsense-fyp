# Recording consent and storage rules (draft for the owner and the supervisor)

Status: **draft, not approved.** It must be read by the owner and, if the university requires it, by the ethics contact, before any recording of a person is made. Nothing in the project records a person yet.
It applies to the role-play recordings of p1-ai-infrastructure-s04, the voice tests of p1-voice-input and the accented-speech tests of the recogniser (DT-205).

## Consent form (one per participant, read aloud and signed or recorded before the session)

> **ProcSense speech recording: what happens to your voice**
> 1. You are asked to say sentences a trainee might say to a patient or a colleague in a made-up hospital scene. Everyone in the scene is invented. You are not tested; no mark or note about you is kept.
> 2. The sessions are recorded as audio. The recordings are used only to measure how well the project's speech recogniser and sentence classifier work, and to build a small test set of sentences.
> 3. Your name is not stored with the recording. The recording carries a code (for example `P07`). The list that links codes to names is kept separately by the owner and deleted when the project ends.
> 4. The recordings and their transcripts are stored on the project's own machines, not in the public repository, and not on any outside speech or language-model service.
> 5. You can stop at any time and ask for your recordings to be deleted, without giving a reason, until the test set is frozen (the date is on this form). After that the sentences cannot be taken out of a frozen set, but your audio will still be deleted.
> 6. Taking part is voluntary and has no effect on your studies or your work.
>
> Name: ______ Code: ______ Date (UTC): ______ Signature or recorded yes: ______ Contact for deletion: ______

## Storage rules

| Rule | Detail |
|---|---|
| Where | Encrypted disk on the owner's machine, folder outside the repository (never `content/`, `tests/` or `artifacts/` of the repo). The repository holds only the transcripts that became test sentences, with the speaker code, and never the audio. |
| Who | The owner and the labellers named for the set. No one else. |
| Identity | A code per speaker; the key (code to name) is a separate file kept by the owner only. |
| Labellers | They see transcripts or audio by code; they sign that they will not share them. |
| Outside services | No recording or transcript is sent to a hosted speech or language-model service. Local models only (ADR-0010). |
| Retention | Audio deleted when the project's evaluation ends or at the participant's request, whichever is first; the code key deleted at the same time. |
| Logging | Timestamps in UTC. A log records who opened which set and when. |
| Frozen sets | A frozen test set carries a hash; deleting a participant's audio does not change the hash of the transcript file, which holds no identity. |
| Children, patients, staff on duty | Not recorded. Participants are adult students and colleagues acting a role. |

## Open items

1. The owner or supervisor confirms whether the university's ethics process applies to recordings of adult volunteers acting invented scenes (DT-210).
2. Who the labellers are (two, plus an adjudicator who is a third person).
3. Where the encrypted folder will be.

# Records usability: the task script (p0-patient-records-emr-s06)

The script every tester gets, word for word, so the times can be compared (EXP-REC-1 to EXP-REC-3). Results go in [results.md](results.md). The session is run on the Quest with the Demo build; the headset check that frames it is [UX-P0-EMR](../../../testing/SANNAN_EXTERNAL_TESTING.md#ux-p0-emr).

## Before each tester

- A tester who has not used ProcSense before. Note whether they have used a hospital EMR.
- The same 30-second orientation for everyone, read out: "This is a hospital ward. The patient's paper folder is on the bed table and the computer at the desk is the electronic record. Pick things up with the grip button and press buttons by pointing and pulling the trigger, or by touching them. I will give you four small tasks. Tell me when you think you are done. There is no score."
- A stopwatch; start it when you finish reading the task, stop it when the tester says the answer (or says they are done) or at 3 minutes.
- Note every wrong turn (a wrong tab, a wrong patient, a page turned the wrong way) and every time the tester asks for help.

## Set-up per task

| Task | Case | Set-up before the tester starts |
|---|---|---|
| 1 | VAR-S1-A | Session started, identity confirmed (the Session tab of the tablet). The EMR shows the ward list. |
| 2 | VAR-S1-C | HbA1c ordered and back: order HbA1c from the tablet, take the sample, then let time pass on the tablet until the result is available. (The HbA1c is a result of this admission, so it is in Lab results only once it is back.) |
| 3 | VAR-S1-PB | LFT ordered, taken and delivered: the printout lies on the desk tray. |
| 4 | VAR-S1-A | The consent form signed (the Consent tab of the tablet), so it is in the folder. |

## The tasks (read exactly)

1. "Find Mrs Khan's previous ultrasound report and tell me what it found." (EXP-REC-1: search or the ward list, the Imaging tab, the report.)
2. "What was the last HbA1c for Mrs Nasreen Akhtar?" (EXP-REC-1: the Lab results tab.)
3. "Add the new LFT report to the patient's folder." (EXP-REC-3: pick up the printout, drop it into the folder's report pocket, find it as a page.)
4. "Open the patient's folder and read me the consent form." (EXP-REC-3: open the folder, turn to the consent form. The form is one page in this build, so the task asks for the form, not a page number.)

## After the tasks

- EXP-REC-2: standing at the desk, ask the tester to read three lines of the EMR's Lab results aloud. Note whether they read them without leaning in, and if not, how far from the screen they had to come. (The EMR has no text-size setting yet; the size is chosen from these notes and then fixed in the UI guidelines.)
- Ask: "What was the hardest part?" Write the answer down in their words.

## Pass marks

- EXP-REC-1: median time for tasks 1 and 2 at most 30 s.
- EXP-REC-2: one text size chosen for the EMR, the size at which every tester read the lines.
- EXP-REC-3: tasks 3 and 4 done without help.

Then fix the top three issues (for example larger tab labels, the ward list order, the page-turn grab zone) and run the four tasks again with two new testers.

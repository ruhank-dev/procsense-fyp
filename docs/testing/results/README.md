# Result files for external tests

One file per item of [../SANNAN_EXTERNAL_TESTING.md](../SANNAN_EXTERNAL_TESTING.md), named `<ID>.md` (for example `DC-01.md`, `UX-P0-VRUI.md`, `EX-03.md`), with logs, screenshots and videos in a folder of the same name (`DC-01/`). The first line must be `Result: PASS`, `Result: FAIL` or `Result: NOT RUN`. Then, in this order: the ID; date and time in UTC; who ran it; the device and build, the git commit, the account or the person; the numbers measured against the pass rule; the evidence file names; notes.

Do not commit anything that identifies a participant, a consent form, a signature, a password or a token. Consent forms stay with the owner; the result file says only that consent was obtained and by whom it is held.

After adding or changing a result file run `uv run --project tools tools/plans/external_testing.py` so the page shows the new status, and commit both. No result file exists until a person has run the item.

# p1-backend-s10 — Quest sign-in and device-code login (EXP-BE-4)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
A login page when the VR application starts. First version: email and password on the headset (the account system's own sign-in, through the VR keyboard). Second version: a device code. The headset shows a short code and a link or QR code, the person signs in on a phone or computer (where typing is easy and the password manager works) and approves the code, and the headset is signed in. The service issues the headset its own short-lived access token and a refresh token that can be revoked. A remembered login lets practice start offline.

## Why
Typing a password in VR is slow and error-prone, but the application needs a real login: results belong to a person in an organisation. A device code is the standard answer for devices without a keyboard.

## How to implement
1. Server, device-code flow: `POST /auth/device/start` returns a device code, a user code (short, easy to read, no look-alike characters), the verification address and a polling interval; `POST /auth/device/token` is polled by the headset (rate-limited; it answers pending, denied, expired or the tokens); the page `/device` (web dashboard, `p1-backend-s11`) asks the signed-in person to type or confirm the code and approve it. Codes expire in minutes and work once.
2. Device sessions: a table of refresh tokens stored only as hashes, tied to the person, the organisation and a device name; access tokens are short (minutes); refresh tokens rotate on each use and expire after a period of inactivity; a person or an organisation admin can revoke a device. The service accepts both kinds of token (the account system's for the web, its own for headsets) in the same login dependency (`p1-backend-s08`).
3. Unity (`Scripts/Auth/`): `SignInScreen` (form and device-code views), `AuthService`, `TokenStore` (the refresh token in an encrypted file under the application's private storage, key kept in the Android keystore through a small plugin; decide the final method in EXP-BE-4, keeping the rule that no token is ever in plain text in logs or preferences), a sign-out that revokes on the server when online.
4. Offline rules (owner decision, DT-126; proposal): practice can start offline with a remembered login that is still valid; assessment mode needs an online check when it starts (a valid login, the case approved for assessment, the current content version) and then plays offline.
5. Clock safety: the headset's date can be wrong → expiry is computed from the server's `expires_in` and a monotonic timer, not from the device date.
6. Play Mode tests with a mock server: password sign-in, device code approved, denied and expired, refresh, revoked token, offline start with a remembered login, assessment refused offline.
7. EXP-BE-4: five people sign in with each method; record times and problems; keep the method most people prefer as the default.

## Verification
T-DV1 (a code works once and expires; polling too fast is refused; a code approved by one account cannot be claimed as another), T-DV2 (a revoked device is refused on the next request; a stolen refresh token used twice is detected and the family revoked), T-DV3 (offline behaviour as decided) pass; EXP-BE-4 median under 60 s.

## Files changed
`server/routers/device_auth.py`, `server/models/device_session.py`, `server/auth/device_tokens.py`, Alembic migration, `server/tests/test_device_auth.py`; `unity/Assets/Scripts/Auth/*`, Play Mode tests.

## Dependencies
p1-backend-s08; p0-vr-platform-s02 (the Unity content and platform setup); the Unity project (Sannan).

## Common issues
Tokens in logs or in `PlayerPrefs` are readable → never. Polling too often is rate-limited → follow the interval the server returns. The VR keyboard hides the password poorly in front of other people → the device code keeps the password off the headset. An enterprise Wi-Fi sign-in page can block the first request → test on a hotspot first.

## Sources
ADR-0009; the OAuth device authorisation grant (RFC 8628) as the model for the flow; Supabase Auth documentation.

## Implementation notes, server half (14 Oct 2026)
This part is the server only. The headset side (`unity/Assets/Scripts/Auth/*`, the keystore plugin, the sign-in screen, the Play Mode tests) is Sannan's and is not built; the password-on-headset method (version one in "What") needs no server change: the headset signs in to the account system directly and sends that token, which the service already accepts. EXP-BE-4 (five people, each method) needs a headset and is not done.
- **Flow (as RFC 8628):** `POST /auth/device/start` (device name) returns a device code (secret, kept by the headset), a user code (8 letters and digits shown as `BCDF-GHJK`, no vowels and no look-alikes), the address to visit, the lifetime (10 minutes) and the poll interval (5 seconds). A person signed in on the web sends `POST /auth/device/decision` with the code and approve or deny. The headset polls `POST /auth/device/token`: it is told `authorization_pending`, `slow_down`, `access_denied` or `expired_token` (HTTP 400, the OAuth words; an unknown code looks expired) or receives its tokens once. The `/device` page itself belongs to p1-backend-s11.
- **Tokens:** the headset's own, random and opaque (`psd_` access, 15 minutes; `psr_` refresh, 30 days of not being used, swapped on every use). Only SHA-256 hashes are stored (`device_login`, `device_session`, migration `0005`, row-level security on). The same login check accepts both kinds, so no route changed; a headset's access token is looked up in the database on every request, which is why a revoked headset is refused on its very next call. Expiry is given as `expires_in` seconds so the headset can count with its own timer and not its date.
- **Decisions that are the project team's, not the book's:** the headset's token works for training only: administering an organisation (supervisor and above), approving other headsets, listing devices and changing the account need a web sign-in (otherwise a lost headset of an administrator would be an administrator); a refresh token used a second time revokes that headset's whole sign-in, and only the one previous token is remembered (an older one is refused without revoking); the tokens are issued to whoever approved the code, never to whoever asked; own opaque tokens rather than signed ones (nothing to rotate or leak, revocation is a lookup); 30 code starts an hour per caller address and 30 decisions an hour per person, in memory like the upload limit; an organisation administrator revokes by deactivating the member (the headset then loses the organisation), a separate "revoke this person's headsets" button is a follow-up (DT-143).
- **Offline rules (DT-126):** the server needs no change for them (the headset keeps its refresh token and starts practice without the server; assessment asks the server to confirm at the start); the decision stays with the owner and is for Sannan's side.
- **Tests:** 23 in `server/tests/test_device_auth.py` on PostgreSQL 17: T-DV1 and T-DV2 as written, plus the rules above. T-DV3 (offline) and EXP-BE-4 belong to the Unity half.

## Headset half, first part (14 Oct 2026)
- **Built (on Sannan's login screen, unity/):** email and password sign-in against the live service (`AuthService`, `Http`, `ServiceSettings`), the service address as a build setting, the screen's messages for a wrong password, no connection, no organisation and a missing privacy answer, the token kept in memory and renewed, the offline demo account kept as an explicit setting. 12 EditMode and 5 PlayMode tests pass in Unity batch mode.
- **Decisions that are the project team's, not the book's:** the first version is the plan's "version one" (email and password through the form Sannan built), not the device code; the password goes straight to the account system and the service never sees it; the headset refuses to continue for a person who has not agreed to the storing of results and sends them to the web page (it cannot answer for them); the fixed demo account stays for offline practice behind a setting that a release build switches off, and it never carries an identity to the service.
- **Not built:** the device-code view, the token store (encrypted, key in the keystore) and a remembered login (so a reload of the app signs in again), assessment mode needing an online check (DT-126), and EXP-BE-4. Quest typing and reach need a device.


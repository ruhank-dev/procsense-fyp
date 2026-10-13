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

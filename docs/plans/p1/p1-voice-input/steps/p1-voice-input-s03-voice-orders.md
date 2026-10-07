# p1-voice-input-s03 — Voice orders with read-back (EXP-VOX-4)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Parse transcripts into ORDER_TEST / REQUEST_MEDICATION / HOLD_MED / REQUEST_TRANSPORT / CALL_STAFF with slots via grammar + fuzzy match (threshold 85) against content, LLM fallback only if grammar fails; numbers/units stripped (no doses); always read back ('Confirm: hold metformin?') before any Core action.

## Why
Spoken orders teach closed-loop communication, but speech recognition makes mistakes — the read-back step guarantees no action happens on a misheard order.

## How to implement
1. `server/orders/grammar.py` + synonym table; LLM fallback schema `order_parse.v1`.
2. Read-back via staff voice; confirm by voice or tablet.
3. Ambiguous drug → clarification question (T-VO2); no match → menu suggestion (T-VO4).
4. Read-back line spoken by the nurse/staff voice; confirmation accepted by voice ('yes', 'confirm') or tablet button; 'no' cancels and offers the menu.
5. Every parsed order and its confirmation logged (intent, slots, confirmed by, latency).

## Verification
T-VO1–T-VO4 pass; no Core action without confirmation (log check).

## Files changed
`server/orders/grammar.py`, `server/orders/synonyms.csv`, `server/schemas/order_parse.py`, `unity/Assets/_Project/Scripts/Voice/ReadBack.cs`, tests.

## Dependencies
p1-voice-input-s02.

## Common issues
Brand names → synonym table.

## Sources
Implementation Guide B-M8, T-VO1–T-VO4.

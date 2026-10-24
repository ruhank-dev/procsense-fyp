"""Who takes the patient home and stays with them (DT-178): synthetic practice wording authored 24 Oct 2026 for the 16 P0 case files that stated none.

The patient speaks (wording "quote"). Planned (S1) day-case patients name an adult who takes them home and stays the first night (the usual day-surgery requirement: a responsible adult
for the first 24 hours; a British Association of Day Surgery leaflet reproduced by an NHS trust, recorded as R-051 in docs/research/research_log.md). Emergency (S2) patients are admitted, so nobody takes them home
today; they name the relative who is on the way. Invented people, practice material only, not clinician-reviewed (ADR-0008: not a gate).
"""
RESPONSIBLE_ADULT = {
    "VAR-S1-B": ("son", True, True, "My son will take me home and stay with me tonight."),
    "VAR-S1-C": ("husband", True, True, "My husband is taking the day off. He will drive me home and stay the night."),
    "VAR-S1-D": ("daughter", True, True, "My daughter will pick me up and sleep over at mine."),
    "VAR-S1-E": ("sister", True, True, "My sister is coming for me and she will stay with me tonight."),
    "VAR-S1-G": ("mother", True, True, "My mum will take me home and stay a couple of nights."),
    "VAR-S1-H": ("partner", True, True, "My partner will bring me home and stay with me tonight."),
    "VAR-S1-J": ("wife", True, True, "My wife will drive me home and stay with me overnight."),
    "VAR-S1-PB": ("husband", True, True, "My husband will take me home and look after me tonight."),
    "STK-S1-D": ("daughter", True, True, "My daughter will take me home when I am discharged and stay with me for the first night."),
    "VAR-S2-B": ("friend", False, False, "I will be staying in hospital. My friend is on her way to be with me."),
    "VAR-S2-D": ("mother", False, False, "I will be admitted, so I will not go home tonight. My mum is on her way in."),
    "VAR-S2-F": ("mother", False, False, "I will not be going home tonight. My mum is coming to the hospital."),
    "VAR-S2-G": ("son", False, False, "I am staying in. My son is coming after work."),
    "VAR-S2-PC": ("father", False, False, "I will be staying in. My dad is on his way here."),
    "VAR-S2-PD": ("wife", False, False, "I will be admitted. My wife is on her way to the hospital."),
    "STK-S2-B": ("wife", False, False, "I will be staying in hospital. My wife is coming in now."),
}

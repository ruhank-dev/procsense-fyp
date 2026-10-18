"""Runs Pulse scenarios and records them as traces for the Core (p1-pulse-physiology-s02): `python tools/pulse/pulse_bridge.py run [--scenario ID] --pulse-install DIR [--dll-dir DIR]` and `... check`.

Pulse (Kitware, Apache-2.0) is not a dependency of the repository: it was built on this machine (tools/pulse/README.md) and is found through --pulse-install (its `install` folder: bin, python, states). `run` needs the Python
interpreter that matches the build (the PyPulse module) and starts the engine from a saved state, applies the scenario's actions at their times (content/physio/pulse_mapping.json), samples the mapped outputs and
writes content/physio/pulse_traces/<scenario>.json: the engine's version and git hash, the state file, the sampling step, the actions, the samples, and the wall time Pulse took per simulated second on this machine.
A session never calls Pulse: PulseTraceEngine in the Core replays the recorded trace, so the values are logged data and a replay is identical. `check` needs no Pulse: it validates the mapping file against the Core's effects
and variables and every committed trace against the mapping.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAPPING = ROOT / "content" / "physio" / "pulse_mapping.json"
TRACES = ROOT / "content" / "physio" / "pulse_traces"
CORE_VARS = {"HR", "SBP", "DBP", "RR", "SpO2", "Temp"}


def mapping() -> dict:
    return json.loads(MAPPING.read_text(encoding="utf-8"))


def problems() -> list[str]:
    m = mapping()
    out = []
    effects = {e["id"] for e in json.loads((ROOT / "content" / "physio" / "effects.json").read_text(encoding="utf-8"))["effects"]}
    model_vars = {v["id"] for v in json.loads((ROOT / "content" / "physio" / "model.json").read_text(encoding="utf-8"))["variables"]}
    for o in m["outputs"]:
        if o["core"] not in model_vars:
            out.append(f"output {o['request']} maps to {o['core']}, which the Core model does not have")
    mapped_cores = {o["core"] for o in m["outputs"]}
    if mapped_cores != CORE_VARS:
        out.append(f"outputs cover {sorted(mapped_cores)}, expected {sorted(CORE_VARS)}")
    for eid in list(m["effects"]) + list(m["patterns"]):
        if eid not in effects:
            out.append(f"{eid} is not an effect or pattern of content/physio/effects.json")
    for eid in effects:
        if eid not in m["effects"] and eid not in m["patterns"] and eid not in m["unmapped"]:
            out.append(f"{eid} is neither mapped nor listed as unmapped with a reason")
    for s in m["scenarios"]:
        for ev in s["pulse_events"]:
            if ("effect" in ev) == ("pattern" in ev):
                out.append(f"{s['id']}: an event needs exactly one of effect or pattern")
            key = ev.get("effect") or ev.get("pattern")
            if key not in m["effects"] and key not in m["patterns"]:
                out.append(f"{s['id']}: event names {key}, which is not mapped")
        for ev in s["lite_events"]:
            if ev["id"] not in effects:
                out.append(f"{s['id']}: LitePhysio event {ev['id']} does not exist")
        if set(s["baseline"]) != CORE_VARS:
            out.append(f"{s['id']}: baseline must give {sorted(CORE_VARS)}")
    if TRACES.exists():
        listed = json.loads((TRACES / "index.json").read_text(encoding="utf-8"))["traces"] if (TRACES / "index.json").exists() else []
        if listed != sorted(f.stem for f in TRACES.glob("*.json") if f.stem != "index"):
            out.append("index.json does not list exactly the traces in the folder: run pulse_bridge.py run")
    for f in sorted(TRACES.glob("*.json")) if TRACES.exists() else []:
        if f.name == "index.json":
            continue
        t = json.loads(f.read_text(encoding="utf-8"))
        if not any(s["id"] == t["scenario"] for s in m["scenarios"]):
            out.append(f"{f.name}: scenario {t['scenario']} is not in the mapping")
        if t["samples"] and set(t["samples"][0]) != CORE_VARS | {"t"}:
            out.append(f"{f.name}: samples must carry t and {sorted(CORE_VARS)}")
        if any(b["t"] <= a["t"] for a, b in zip(t["samples"], t["samples"][1:])):
            out.append(f"{f.name}: sample times must increase")
    return out


def action_for(key: str, m: dict):
    """The Pulse action object for a mapped effect or pattern."""
    from pulse.cdm.patient_actions import SEAcuteStress, SEHemorrhage, SESubstanceBolus, SESubstanceCompoundInfusion, SESupplementalOxygen, eDevice, eHemorrhage_Compartment, eSubstance_Administration
    from pulse.cdm.scalars import MassPerVolumeUnit, VolumePerTimeUnit, VolumeUnit
    spec = m["effects"].get(key) or m["patterns"][key]
    kind = spec["pulse"]
    if kind == "Hemorrhage":
        a = SEHemorrhage()
        a.set_compartment(getattr(eHemorrhage_Compartment, spec["compartment"]).name)
        a.get_severity().set_value(spec["severity"])
    elif kind == "SubstanceCompoundInfusion":
        a = SESubstanceCompoundInfusion()
        a.set_compound(spec["compound"])
        a.get_rate().set_value(spec["rate_ml_per_min"], VolumePerTimeUnit.mL_Per_min)
        a.get_bag_volume().set_value(spec["bag_ml"], VolumeUnit.mL)
    elif kind == "SupplementalOxygen":
        a = SESupplementalOxygen()
        a.set_device(getattr(eDevice, spec["device"]))
        a.get_flow().set_value(spec["flow_l_per_min"], VolumePerTimeUnit.L_Per_min)
        a.get_volume().set_value(spec["volume_l"], VolumeUnit.L)
    elif kind == "SubstanceBolus":
        a = SESubstanceBolus()
        a.set_admin_route(eSubstance_Administration.Intravenous)
        a.set_substance(spec["substance"])
        a.get_dose().set_value(spec["dose_ml"], VolumeUnit.mL)
        if "concentration_mg_per_ml" in spec:
            a.get_concentration().set_value(spec["concentration_mg_per_ml"], MassPerVolumeUnit.mg_Per_mL)
        else:
            a.get_concentration().set_value(spec["concentration_ug_per_ml"], MassPerVolumeUnit.ug_Per_mL)
    elif kind == "AcuteStress":
        a = SEAcuteStress()
        a.get_severity().set_value(spec["severity"])
    else:
        raise SystemExit(f"no Pulse action for {kind}")
    return a


def run_scenario(s: dict, m: dict, install: Path) -> dict:
    from pulse.cdm.engine import SEDataRequest, SEDataRequestManager
    from pulse.cdm.scalars import FrequencyUnit, PressureUnit, TemperatureUnit
    from pulse.engine.PulseEngine import PulseEngine, git_hash, version
    units = {"Per_min": FrequencyUnit.Per_min, "mmHg": PressureUnit.mmHg, "degC": TemperatureUnit.C}
    reqs = [SEDataRequest.create_physiology_request(o["request"], unit=units[o["unit"]]) if o["unit"] else SEDataRequest.create_physiology_request(o["request"]) for o in m["outputs"]]
    pulse = PulseEngine()
    if not pulse.serialize_from_file(str(install / "bin" / s["start_state"]), SEDataRequestManager(reqs)):
        raise SystemExit("cannot load " + s["start_state"])
    events = sorted(s["pulse_events"], key=lambda e: e["t"])
    samples, wall, t = [], 0.0, 0
    step = s["sample_every_s"]

    def record(now):
        row = pulse.pull_data()                                   # [simulation time, one value per request]
        samples.append({"t": now, **{o["core"]: round(row[1 + i] * o["scale"], 3) for i, o in enumerate(m["outputs"])}})

    record(0)
    pending = list(events)
    while t < s["duration_s"]:
        while pending and pending[0]["t"] <= t:
            pulse.process_action(action_for(pending[0].get("effect") or pending[0]["pattern"], m))
            pending.pop(0)
        started = time.perf_counter()
        pulse.advance_time_s(step)
        wall += time.perf_counter() - started
        t += step
        record(t)
    return {"scenario": s["id"], "engine": {"name": "Pulse Physiology Engine", "version": version(), "git_hash": git_hash(), "build": m["pulse_build"]}, "state_file": s["start_state"], "sample_every_s": step,
            "actions": [{"t": e["t"], "key": e.get("effect") or e.get("pattern")} for e in events], "wall_seconds_per_simulated_second": round(wall / s["duration_s"], 5),
            "recorded_on": "the development desktop (Windows, MinGW-w64 build), processor shared with other work; a cost figure for this machine only", "samples": samples}


def write_index() -> None:
    """index.json lists the traces the Core loads (it has no way to list a folder)."""
    ids = sorted(f.stem for f in TRACES.glob("*.json") if f.stem != "index")
    (TRACES / "index.json").write_text(json.dumps({"traces": ids}, indent=1) + "\n", encoding="utf-8", newline="\n")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("command", choices=["run", "check"])
    ap.add_argument("--scenario", default="")
    ap.add_argument("--pulse-install", default="")
    ap.add_argument("--dll-dir", default="", help="a folder with the runtime DLLs of the compiler that built Pulse (Windows)")
    a = ap.parse_args(argv)
    if a.command == "check":
        bad = problems()
        for b in bad:
            print("FAIL " + b)
        print(f"pulse mapping {'FAILED' if bad else 'valid'} ({len(mapping()['scenarios'])} scenarios, {len([f for f in TRACES.glob('*.json') if f.stem != 'index']) if TRACES.exists() else 0} traces)")
        return 1 if bad else 0
    install = Path(a.pulse_install or os.environ.get("PULSE_INSTALL", ""))
    if not (install / "bin").exists():
        raise SystemExit("give --pulse-install (the install folder of a Pulse build)")
    if hasattr(os, "add_dll_directory"):
        for d in (a.dll_dir, str(install / "bin")):
            if d:
                os.add_dll_directory(d)
    sys.path.insert(0, str(install / "python"))
    sys.path.insert(0, str(install / "bin"))
    os.chdir(install / "bin")
    m = mapping()
    TRACES.mkdir(exist_ok=True)
    for s in m["scenarios"]:
        if a.scenario and s["id"] != a.scenario:
            continue
        tr = run_scenario(s, m, install)
        (TRACES / f"{s['id']}.json").write_text(json.dumps(tr, indent=1) + "\n", encoding="utf-8", newline="\n")
        write_index()
        print(f"{s['id']}: {len(tr['samples'])} samples, {tr['wall_seconds_per_simulated_second'] * 1000:.1f} ms of wall time per simulated second")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

"""Build Synverk flow diagrams (schema /api/flows/schema, flowchart layout) from docs/architecture/procsense_full_pipeline.json.

Used by pipeline_docs.py (which writes and checks the files in docs/architecture/flows/). Nothing here is hand-edited: change the pipeline JSON and regenerate.

One flowchart holds at most 30 nodes, 60 edges and 6 notes (schema read 12 Oct 2026), so the 168-node pipeline is split into linked diagrams:
  00 whole live pipeline (every stage LG-01..21, learner profile, entries, exits, fallback, offline preparation as one node)
  01 offline preparation and datasets · 02 models and the stages they serve · 03 components and the stages they serve
  04 roadmap phases · 05..09 AI features by category (A to E) with their dependencies
Every limit is checked when a file is built; a diagram that would break one raises an error instead of being cut silently.
"""
import json
from collections import defaultdict

MAX_BYTES = 64 * 1024
LIMITS = dict(nodes=30, edges=60, notes=6, note=220, title=80, subtitle=220, ntitle=48, text=180, label=28, id=24)
ACTORS = set("person team synverk agent model google web phone api code terminal branch bug test rocket package chip cloud database queue sync bolt link search globe check lock key security warning eye trash card file folder book image chart calendar clock flag star idea heart gear mail chat bell money cart home pin hammer axe spear sword horn rune longship raven wolf serpent tree bridge helm ring mead fire frost sun moon valknut".split())
TONES = {"blue", "aqua", "mint", "sun", "coral", "pink", "grey", "paper"}
TONE = {"Implemented and tested": "mint", "Implemented but incompletely tested": "sun", "Partially implemented": "sun", "Planned": "blue", "Blocked": "coral",
        "Deferred": "grey", "Proposed new feature": "aqua", "Superseded or rejected": "pink"}
SHORT = {"Implemented and tested": "Built", "Implemented but incompletely tested": "Built, part tested", "Partially implemented": "Partly built", "Planned": "Planned", "Blocked": "Blocked",
         "Deferred": "Deferred", "Proposed new feature": "Proposed", "Superseded or rejected": "Rejected"}
STAGE_ACTOR = {"LG-01": "link", "LG-02": "idea", "LG-03": "search", "LG-04": "agent", "LG-05": "file", "LG-06": "model", "LG-07": "chart", "LG-08": "image", "LG-09": "check", "LG-10": "security",
               "LG-11": "eye", "LG-12": "hammer", "LG-13": "star", "LG-14": "package", "LG-15": "rocket", "LG-16": "chip", "LG-17": "chat", "LG-18": "test", "LG-19": "book", "LG-20": "database",
               "LG-21": "sync", "LP-01": "eye", "LP-02": "sync", "LP-03": "idea", "ENTRY-TRAINEE": "person", "ENTRY-AUTHOR": "team", "EXIT-SESSION-REPORT": "flag", "EXIT-OFFLINE-RELEASE": "flag",
               "FALLBACK-POOL-LIBRARY": "warning"}
KIND_ACTOR = {"stage": "gear", "offline_step": "folder", "component": "code", "store": "database", "dataset": "book", "model": "model", "phase": "calendar", "runtime_step": "idea",
              "entry": "person", "exit": "flag", "fallback": "warning"}
FEATURE_ACTOR = {"ai": "model", "hybrid": "agent", "deterministic": "gear"}
FLOW_TYPES = {"data_flow", "control", "repair", "fallback", "feedback"}


def trunc(s: str, n: int) -> str:
    s = " ".join(str(s).split())
    return s if len(s) <= n else s[: n - 1].rstrip(" ,;:.") + "…"


TITLES = {"EXIT-OFFLINE-RELEASE": "Reviewed offline release", "OFF-04": "Block library (requires/excludes)", "OFF-08": "Model preparation (no training)", "LG-04": "AI plan composer + constraint solver",
          "LG-06": "AI narrative, profile and persona", "LG-08": "Image and waveform handling", "LG-10": "Stage B validation (clinical safety)", "CMP-SCORER": "Scorer, evidence, debrief facts",
          "MC-01": "Scenario composition (narrative, Stage A)", "E64": "Generative medical image synthesis", "A7": "Adversarial trap scenarios", "A13": "Provenance and reproducibility",
          "B16": "Ultrasound selection and synthesis", "E69": "AI inference and dual-GPU deployment"}


def short_title(i: str, label: str) -> str:
    import re
    if i in TITLES:
        return TITLES[i]
    t = label.split(" ", 1)[-1] if i[:3] in ("DS-", "MC-") else label
    if len(t) > 48:
        t = re.sub(r"\s*\([^()]*\)\s*$", "", t)
    return trunc(t, 48)


def clip(s: str, n: int) -> str:
    """Shorten at a clause boundary where possible so the text reads whole, not cut mid-phrase."""
    s = " ".join(str(s).split())
    if len(s) <= n:
        return s
    cut = s[: n - 1]
    best = max(cut.rfind(", "), cut.rfind("; "), cut.rfind(": "), cut.rfind(" ("), cut.rfind(" and "), cut.rfind(" with "))
    if best >= int(n * 0.55):
        return cut[:best].rstrip(" ,;:(") + "…"
    return cut[: cut.rfind(" ")].rstrip(" ,;:(") + "…"


def first_sentence(s: str) -> str:
    s = " ".join(str(s).split())
    i = s.find(". ")
    return s if i < 0 else s[: i + 1]


def check_flow(flow: dict, name: str) -> None:
    L = LIMITS
    ids = [n["id"] for n in flow["nodes"]]
    if len(ids) != len(set(ids)):
        raise ValueError(f"{name}: duplicate node ids")
    if not 1 <= len(ids) <= L["nodes"]:
        raise ValueError(f"{name}: {len(ids)} nodes (limit {L['nodes']})")
    if len(flow.get("edges", [])) > L["edges"]:
        raise ValueError(f"{name}: {len(flow['edges'])} edges (limit {L['edges']})")
    if len(flow.get("notes", [])) > L["notes"]:
        raise ValueError(f"{name}: too many notes")
    if len(flow["title"]) > L["title"] or len(flow.get("subtitle", "")) > L["subtitle"]:
        raise ValueError(f"{name}: title or subtitle too long")
    for n in flow["nodes"]:
        if not (1 <= len(n["id"]) <= L["id"]) or not all(c.isalnum() or c in "_-" for c in n["id"]):
            raise ValueError(f"{name}: bad id {n['id']}")
        if len(n["title"]) > L["ntitle"] or len(n.get("text", "")) > L["text"] or len(n.get("label", "")) > L["label"]:
            raise ValueError(f"{name}: node {n['id']} field too long")
        if n.get("actor") not in ACTORS or n.get("tone", "paper") not in TONES:
            raise ValueError(f"{name}: node {n['id']} actor or tone not in the schema")
    for e in flow.get("edges", []):
        if e["from"] not in ids or e["to"] not in ids:
            raise ValueError(f"{name}: edge {e['from']}->{e['to']} has an endpoint outside the diagram")
        if len(e.get("label", "")) > L["label"]:
            raise ValueError(f"{name}: edge label too long")
    for t in flow.get("notes", []):
        if len(t) > L["note"]:
            raise ValueError(f"{name}: note too long")
    size = len(json.dumps(flow, indent=2, ensure_ascii=False).encode("utf-8"))
    if size >= MAX_BYTES:
        raise ValueError(f"{name}: {size} bytes (limit {MAX_BYTES})")


def feature_ids(n):
    return ", ".join(n["traced_to"]["features"][:4]) if n.get("traced_to") else ""


def node_text(n) -> str:
    status = SHORT.get(n.get("status"), n.get("status", ""))
    tail = ""
    if n["type"] not in ("feature", "dataset", "model"):
        fs = feature_ids(n)
        tail = f" Plans: {fs}." if fs else ""
    return clip(f"{status}. {first_sentence(n.get('description', ''))}", 180 - len(tail)) + tail


def make(nodes_by_id, ids, extra_nodes=()):
    out = []
    for i in ids:
        n = nodes_by_id[i]
        t = n["type"]
        actor = STAGE_ACTOR.get(i) or (FEATURE_ACTOR[n["kind"]] if t == "feature" else KIND_ACTOR.get(t, "gear"))
        label = i if t != "feature" else f"{i} {n['tier']} {n['phase']}"
        out.append(dict(id=i, actor=actor, label=trunc(label, 28), title=short_title(i, n["label"]), text=node_text(n), tone=TONE.get(n.get("status"), "paper")))
    out.extend(extra_nodes)
    return out


def edges_among(doc, ids, types, label_for=None):
    keep = set(ids)
    seen, out = set(), []
    for e in doc["edges"]:
        if e["type"] in types and e["source"] in keep and e["target"] in keep and (e["source"], e["target"]) not in seen:
            seen.add((e["source"], e["target"]))
            d = dict(**{"from": e["source"], "to": e["target"]})
            lab = e["label"] or (label_for or {}).get(e["type"], "")
            if lab:
                d["label"] = trunc(lab, 28)
            if e["type"] in ("repair", "fallback", "control"):
                d["label"] = trunc(e["label"] or e["type"], 28)
            out.append(d)
    return out


def notes_for(doc, extra=()):
    md = doc["metadata"]
    return [trunc(x, 220) for x in extra] + [
        "Colour = status: mint built, sun partly built, blue planned, aqua proposed, coral blocked, grey deferred. Planning only (12 Oct 2026): nothing blue, aqua, coral or grey is built.",
        trunc("Generated from docs/architecture/procsense_full_pipeline.json by tools/plans/pipeline_docs.py; edit the JSON, never this file. Each box's label is its id in that file.", 220),
    ]


def build(doc: dict) -> dict:
    nodes = {n["id"]: n for n in doc["nodes"]}
    flows = {}
    SCHEMA = "/api/flows/schema"

    # ---- 00: the whole live pipeline ----
    live = [f"LG-{i:02d}" for i in range(1, 22)] + ["LP-01", "LP-02", "LP-03", "ENTRY-TRAINEE", "ENTRY-AUTHOR", "EXIT-SESSION-REPORT", "EXIT-OFFLINE-RELEASE", "FALLBACK-POOL-LIBRARY"]
    offline_ids = {i for i, n in nodes.items() if n["type"] == "offline_step" or i in ("ST-PACKS", "ST-BLOCKS", "ST-KB")}
    off_node = dict(id="OFFLINE", actor="folder", label="OFF-01..10", title="Offline preparation", tone="blue",
                    text="Dataset analysis, knowledge base, blocks, packs, retrieval and media indices, prompts, model preparation. Runs before sessions; never inside one.")
    ns = make(nodes, live, [off_node])
    es = edges_among(doc, live, FLOW_TYPES | {"produces", "uses_data"})
    es = [e for e in es if not (e["from"] in ("ST-LEARNER",) or e["to"] in ("ST-LEARNER",))]
    seen = {(e["from"], e["to"]) for e in es}
    for e in doc["edges"]:
        s, t = e["source"], e["target"]
        if s in offline_ids and t in live and ("OFFLINE", t) not in seen:
            seen.add(("OFFLINE", t))
            es.append({"from": "OFFLINE", "to": t, "label": "prepared artefacts"})
        if s in ("ENTRY-AUTHOR",) and t in offline_ids and ("ENTRY-AUTHOR", "OFFLINE") not in seen:
            seen.add(("ENTRY-AUTHOR", "OFFLINE"))
            es.append({"from": "ENTRY-AUTHOR", "to": "OFFLINE", "label": "batch run"})
        if s == "LG-21" and t in offline_ids and ("LG-21", "OFFLINE") not in seen:
            seen.add(("LG-21", "OFFLINE"))
            es.append({"from": "LG-21", "to": "OFFLINE", "label": "reviewed release"})
    flows["00-live-pipeline.json"] = dict(**{"$schema": SCHEMA}, title="ProcSense: whole live pipeline",
        subtitle="Learner profile → AI scenario plan → numbers and media → validation and repair → package → simulation → debrief → history → offline feedback. Planning only.", columns=6,
        nodes=ns, edges=es, notes=notes_for(doc, [
            "AI composes, rules enforce: LG-04 proposes the plan (LLM + knowledge-base retrieval); solver, Stage B and cross-modal rules decide validity. Solver-only is the baseline and fallback.",
            "Rules decide state, critical scoring and action acceptance (ADR-0005). Sessions never retrain; the learner profile is updated cheaply afterwards (LP-02)."]))

    # ---- 01: offline preparation and datasets ----
    ds = [i for i, n in nodes.items() if n["type"] == "dataset"]
    off = sorted(i for i, n in nodes.items() if n["type"] == "offline_step")
    ids01 = ds + off + ["ST-PACKS", "ST-BLOCKS", "ST-KB"]
    flows["01-offline-and-datasets.json"] = dict(**{"$schema": SCHEMA}, title="Offline preparation and datasets",
        subtitle="Which dataset feeds which preparation step. Nothing here runs inside a session.", columns=6, nodes=make(nodes, ids01), edges=edges_among(doc, ids01, {"produces", "uses_data", "data_flow"}),
        notes=notes_for(doc, ["DS-0008 (licence unconfirmed) and DS-0013 (no-derivatives) are not used; DS-0009/10 only considered. Licences: docs/research/licence_provenance_register.md."]))

    # ---- 02 models and 03 components with the stages they serve ----
    def with_targets(kind, etype):
        srcs = [i for i, n in nodes.items() if n["type"] == kind]
        tg = sorted({e["target"] for e in doc["edges"] if e["type"] == etype and e["source"] in srcs})
        return srcs + [t for t in tg if t not in srcs]

    ids02 = with_targets("model", "uses_model")
    flows["02-models-and-stages.json"] = dict(**{"$schema": SCHEMA}, title="Models and the stages that use them",
        subtitle="Model catalogue MC-01..13 (candidates, hardware, licence: docs/architecture/model_technology_register.md).", columns=5, nodes=make(nodes, ids02),
        edges=edges_among(doc, ids02, {"uses_model"}), notes=notes_for(doc, ["Hardware: desktop GTX 1660 SUPER and laptop RTX 4050 (6 GB each), benchmarked with Unity running in p1-ai-infrastructure-s02."]))
    ids03 = with_targets("component", "component_of")
    flows["03-components-and-stages.json"] = dict(**{"$schema": SCHEMA}, title="Existing and planned components",
        subtitle="Which code serves which stage. Mint boxes exist and are tested.", columns=5, nodes=make(nodes, ids03), edges=edges_among(doc, ids03, {"component_of"}), notes=notes_for(doc))

    # ---- 04 roadmap ----
    ph = doc["phases"]
    pn = []
    for p in ph:
        pn.append(dict(id=p["id"], actor=KIND_ACTOR["phase"], label=trunc(p["id"] + (" critical path" if p["on_critical_path"] else ""), 28), title=short_title(p["id"], p["name"]),
                       text=clip(f"{SHORT.get(p['status'], p['status'])}. {p['objective']}", 150) + f" {len(p['tasks'])} tasks, {len(p['features'])} features.", tone=TONE.get(p["status"], "paper")))
    pe = [{"from": d, "to": p["id"]} for p in ph for d in p["depends_on"]]
    flows["04-roadmap-phases.json"] = dict(**{"$schema": SCHEMA}, title="Roadmap: phases PH-01..PH-24", subtitle="Dependency order. Critical path: " + " → ".join(doc["metadata"]["critical_path"]), columns=6,
        nodes=pn, edges=pe, notes=notes_for(doc, [trunc("AI critical path: " + " → ".join(doc["metadata"]["ai_critical_path"]) + ". Phase tasks are step files under docs/plans.", 220)]))

    # ---- 05..09 features by category ----
    for letter, name in (("A", "Scenario generation and composition"), ("B", "Data, media and investigations"), ("C", "Dialogue, voice and communication"), ("D", "Assessment, feedback and learning"), ("E", "Platform, evaluation and research")):
        fs = sorted((i for i, n in nodes.items() if n["type"] == "feature" and i[0] == letter), key=lambda x: int(x[1:]))
        fn = make(nodes, fs)
        for n, i in zip(fn, fs):
            f = nodes[i]
            outside = [d for d in f["depends_on"] if d not in fs and d in nodes and nodes[d]["type"] == "feature"]
            caps = len(f["plan"]["capabilities"])
            n["text"] = clip(f"{SHORT[f['status']]}. {first_sentence(f['description'])}", 180 - 40) + trunc(f" {caps} capability plans." + (f" Needs {', '.join(outside)}." if outside else ""), 40)
        fe = [{"from": d, "to": i} for i in fs for d in nodes[i]["depends_on"] if d in fs]
        flows[f"0{5 + 'ABCDE'.index(letter)}-features-{letter.lower()}.json"] = dict(**{"$schema": SCHEMA}, title=f"Features {letter}: {name}"[:80], subtitle="Arrows run from a prerequisite to the feature that needs it. Each feature's full plan: docs/architecture/feature_plans/<ID>.md", columns=5,
            nodes=fn, edges=fe, notes=notes_for(doc))

    for name, f in flows.items():
        check_flow(f, name)
    return flows


def render(flow: dict) -> str:
    return json.dumps(flow, indent=2, ensure_ascii=False) + "\n"

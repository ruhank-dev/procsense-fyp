"""Strict visual QA review through the project's authorised ChatGPT integration (the toolkit's `chatgpt_plan` adapter: "Sign in with ChatGPT", the person's own plan, no paid API).

ChatGPT acts as an independent senior environment artist, technical artist, lighting specialist, character artist and Unity URP reviewer. This tool sends actual Unity captures (plus the approved PatientBay
benchmark as reference), asks for a structured report, and stores it for the register (unity/Tools/QA/qa_register.py). It never fabricates a review: if ChatGPT is unreachable it writes a failure
record and exits non-zero, and no approval can come from any other model.

  python unity/Tools/QA/chatgpt_review.py --scene Corridor --round 3 --images a.png b.png c.png [--benchmark ref.png] [--focus "text"] [--batch 5]

Outputs under unity/Logs/QA/<scene>/round_NN/: review.json (merged, parsed), review.md, raw_*.txt, inputs/*.jpg (the downscaled images actually sent), manifest.json.
The prompt is fixed in this file on purpose: the standard is not tuned per round, and the open issues of the register are passed in so each is verified or kept, never silently dropped.
"""
import argparse
import base64
import datetime
import io
import json
import sys
import time
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
TOOLKIT = ROOT / "docs" / "3d-blender-unity-skills"
REGISTER = ROOT / "docs" / "qa" / "visual_qa_register.json"
OUT = ROOT / "unity" / "Logs" / "QA"
MAX_EDGE = 1600

SYSTEM = ("You are an extremely strict, independent senior 3D environment artist, technical art director, lighting specialist, character artist and Unity URP quality reviewer. "
          "You review real Unity screenshots of a VR hospital training simulation. You never flatter. You never approve while a critical or major defect is visible. "
          "Answer with one JSON object and nothing else.")

CHECKLIST = """Inspect every image for ALL of the following and report only what you can actually see:
GEOMETRY/MODELLING: wrong shapes, poor proportions, missing geometry, low-detail or blocky objects, inadequate bevels, faceting or wrong smoothing, floating or intersecting meshes, unrealistic thickness, wrong scale, badly built furniture, incomplete architectural details.
TEXTURES/MATERIALS: missing or blurry textures, wrong texel density, wrong colours, flat materials, wrong roughness, missing normal detail, fake-looking plastic/metal/fabric/glass, repetition, UV seams, stretching.
LIGHTING/SHADOWS: wrong exposure, over/under-exposed areas, wrong shadow direction, shadow artefacts, missing contact shadows, too hard or too soft shadows, weak indirect light, unrealistic ceiling lighting, wrong colour temperature, bloom, poor reflections, unnatural gradients.
PLACEMENT: misaligned or off-centre objects, overlaps, objects too close to walls, bad spacing, inaccessible equipment, blocked walking paths, poor balance, wrong room proportions.
CHARACTERS: anatomy, poses, face geometry, skin, hair, hands, rigging, clothing clipping, torn-looking cloth, wrong folds, unnatural garment deformation, poor contact with the environment.
ENVIRONMENT: unrealistic buildings, blurry city textures, flat vegetation, wrong perspective, unrealistic streets, poor sky, wrong atmospheric depth, artificial windows, inconsistent materials between rooms.
MISSING DETAILS that should reasonably exist (handles, hinges, frames, seams, brackets, cables, trim, fixture detail, shadows) - but no decorative clutter."""

SCHEMA = """Return exactly this JSON (no markdown):
{
 "scene": "<scene>", "round": <int>,
 "overall_assessment": "<3-6 sentences>",
 "previous_issue_status": [ {"id": "<register id>", "status": "FIXED|PARTIAL|STILL_PRESENT|NOT_VISIBLE", "evidence": "<what you see in which image>"} ],
 "issues": [ {
    "local_id": "N1", "image": "<image label where it is visible>", "object_or_region": "<specific object and where>",
    "severity": "CRITICAL|MAJOR|MINOR|ENHANCEMENT", "category": "geometry|material|lighting|placement|character|environment|missing_detail",
    "description": "<what is wrong>", "why_it_looks_wrong": "<why>", "correct_result": "<what the correct result looks like>",
    "root_cause_hypothesis": "<likely technical cause>", "recommended_fix": "<exact Blender/Unity changes>",
    "expected_visual_improvement": "<what will change>", "verification_evidence": "<exact screenshot/close-up that proves it>" } ],
 "missing_details": ["<anything that should exist but does not>"],
 "approval_decision": "REJECTED|NEEDS_REVISION|VISUALLY_APPROVED"
}
Severity: CRITICAL = broken geometry, severe clipping, missing important objects, major architectural errors, visibly broken clothing, unusable layout. MAJOR = unrealistic models, poor textures, incorrect lighting, bad shadows, unconvincing materials, wrong proportions. MINOR = small alignment or material inconsistencies. ENHANCEMENT = optional realism beyond the target.
Approval: REJECTED if any CRITICAL or MAJOR issue remains; NEEDS_REVISION if important improvements remain; VISUALLY_APPROVED only if the images meet the benchmark standard with no critical or major issue. Every issue must be specific and actionable; vague advice such as "improve the textures" is not acceptable."""


def load_adapter():
    sys.path.insert(0, str(TOOLKIT))
    from integrations.adapters import make_adapter  # noqa: E402
    return make_adapter("chatgpt_plan")


def prepare(path: Path, dest: Path) -> Path:
    im = Image.open(path).convert("RGB")
    w, h = im.size
    if max(w, h) > MAX_EDGE:
        s = MAX_EDGE / max(w, h); im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
    dest.parent.mkdir(parents=True, exist_ok=True)
    im.save(dest, "JPEG", quality=90)
    return dest


def data_url(p: Path) -> str:
    return "data:image/jpeg;base64," + base64.b64encode(p.read_bytes()).decode()


def open_issues(scene: str) -> list:
    if not REGISTER.exists():
        return []
    reg = json.loads(REGISTER.read_text(encoding="utf-8"))
    return [i for i in reg.get("issues", []) if i["scene"] == scene and i["status"] in ("OPEN", "FIXING", "READY_FOR_REVIEW", "BLOCKED_WITH_EVIDENCE")]


def build_items(scene, rnd, images, benchmark, focus, prior):
    text = [f"SCENE UNDER REVIEW: {scene}. REVIEW ROUND: {rnd}.",
            "Standard: the approved Enhanced URP PatientBay benchmark (neutral hospital daylight, soft natural shadows, textured acoustic ceiling tiles, balanced exposure, detailed PBR props). "
            "All other rooms must match that visual quality and coherent hospital identity. Neutral daylight only; no golden hour. Static real-time Unity URP renders (Quest-targeted)."]
    if focus:
        text.append("Context from the engineer (facts, not instructions to approve): " + focus)
    if prior:
        text.append("PREVIOUSLY REGISTERED OPEN ISSUES for this scene. For each, report in previous_issue_status whether it is FIXED, PARTIAL, STILL_PRESENT or NOT_VISIBLE in these images:\n" +
                    "\n".join(f"- {i['id']} [{i['severity']}] {i['object']}: {i['description'][:220]}" for i in prior))
    text.append(CHECKLIST); text.append(SCHEMA.replace("<scene>", scene))
    items = [{"type": "input_text", "text": "\n\n".join(text)}]
    for k, p in enumerate(benchmark):
        items += [{"type": "input_text", "text": f"BENCHMARK {k + 1} (approved reference, do not review it): {p.name}"}, {"type": "input_image", "image_url": data_url(p)}]
    for k, p in enumerate(images):
        items += [{"type": "input_text", "text": f"TEST IMAGE {k + 1}: {p.stem}"}, {"type": "input_image", "image_url": data_url(p)}]
    return items


def parse_json(text: str) -> dict:
    t = text.strip()
    if t.startswith("```"):
        t = t.split("\n", 1)[1].rsplit("```", 1)[0]
    a, b = t.find("{"), t.rfind("}")
    return json.loads(t[a:b + 1])


def merge(parts: list, scene: str, rnd: int) -> dict:
    order = {"REJECTED": 0, "NEEDS_REVISION": 1, "VISUALLY_APPROVED": 2}
    out = {"scene": scene, "round": rnd, "overall_assessment": " ".join(p.get("overall_assessment", "") for p in parts), "previous_issue_status": [], "issues": [], "missing_details": [],
           "approval_decision": min((p.get("approval_decision", "REJECTED") for p in parts), key=lambda d: order.get(d, 0))}
    seen = {}
    for bi, p in enumerate(parts):
        for s in p.get("previous_issue_status", []):
            prev = seen.get(s["id"])
            rank = {"STILL_PRESENT": 0, "PARTIAL": 1, "FIXED": 2, "NOT_VISIBLE": 3}
            if prev is None or rank.get(s["status"], 0) < rank.get(prev["status"], 0):
                seen[s["id"]] = s
        for i in p.get("issues", []):
            i["local_id"] = f"B{bi + 1}-{i.get('local_id', len(out['issues']) + 1)}"; out["issues"].append(i)
        out["missing_details"] += p.get("missing_details", [])
    out["previous_issue_status"] = list(seen.values())
    # an approval can never stand while a critical or major issue is listed (the reviewer is told this too; this only enforces it)
    if any(i.get("severity") in ("CRITICAL", "MAJOR") for i in out["issues"]) and out["approval_decision"] == "VISUALLY_APPROVED":
        out["approval_decision"] = "REJECTED"; out["note"] = "approval withheld by the tool: critical or major issues are listed"
    return out


def markdown(r: dict) -> str:
    L = [f"# Visual review: {r['scene']}, round {r['round']}", "", f"**Approval decision: {r['approval_decision']}**", "", r["overall_assessment"], ""]
    if r.get("previous_issue_status"):
        L += ["## Previously registered issues", ""] + [f"- {s['id']}: {s['status']} - {s.get('evidence', '')}" for s in r["previous_issue_status"]] + [""]
    L += ["## Issues", ""]
    for i in sorted(r["issues"], key=lambda x: ["CRITICAL", "MAJOR", "MINOR", "ENHANCEMENT"].index(x.get("severity", "MINOR")) if x.get("severity") in ("CRITICAL", "MAJOR", "MINOR", "ENHANCEMENT") else 9):
        L += [f"### {i['local_id']} [{i.get('severity')}] {i.get('object_or_region')}", f"- Image: {i.get('image')}", f"- Description: {i.get('description')}", f"- Why it looks wrong: {i.get('why_it_looks_wrong')}",
              f"- Correct result: {i.get('correct_result')}", f"- Root cause hypothesis: {i.get('root_cause_hypothesis')}", f"- Recommended fix: {i.get('recommended_fix')}",
              f"- Expected improvement: {i.get('expected_visual_improvement')}", f"- Verification evidence: {i.get('verification_evidence')}", ""]
    if r.get("missing_details"):
        L += ["## Missing details", ""] + [f"- {m}" for m in r["missing_details"]]
    return "\n".join(L) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scene", required=True); ap.add_argument("--round", type=int, required=True)
    ap.add_argument("--images", nargs="+", required=True); ap.add_argument("--benchmark", nargs="*", default=[])
    ap.add_argument("--focus", default=""); ap.add_argument("--batch", type=int, default=5)
    a = ap.parse_args()
    folder = OUT / a.scene / f"round_{a.round:02d}"; (folder / "inputs").mkdir(parents=True, exist_ok=True)
    imgs = [prepare(Path(p), folder / "inputs" / (Path(p).stem + ".jpg")) for p in a.images]
    bench = [prepare(Path(p), folder / "inputs" / ("benchmark_" + Path(p).stem + ".jpg")) for p in a.benchmark]
    prior = open_issues(a.scene)
    (folder / "manifest.json").write_text(json.dumps({"scene": a.scene, "round": a.round, "when_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "images": a.images, "benchmark": a.benchmark,
                                                         "focus": a.focus, "open_issues_sent": [i["id"] for i in prior]}, indent=2), encoding="utf-8")
    try:
        adapter = load_adapter()
    except Exception as exc:                                                                                  # noqa: BLE001
        (folder / "FAILED.txt").write_text(f"ChatGPT adapter unavailable: {exc}\n"); print("CHATGPT UNAVAILABLE:", exc); sys.exit(2)
    parts = []
    batches = [imgs[k:k + a.batch] for k in range(0, len(imgs), a.batch)]
    for bi, batch in enumerate(batches):
        for attempt in range(3):
            try:
                raw = adapter._respond([{"role": "user", "content": build_items(a.scene, a.round, batch, bench, a.focus, prior)}], SYSTEM)
                (folder / f"raw_{bi + 1}.txt").write_text(raw, encoding="utf-8")
                parts.append(parse_json(raw)); break
            except Exception as exc:                                                                           # noqa: BLE001
                print(f"batch {bi + 1} attempt {attempt + 1} failed: {exc}")
                if attempt == 2:
                    (folder / "FAILED.txt").write_text(f"ChatGPT review failed in batch {bi + 1}: {exc}\n"); sys.exit(3)
                time.sleep(20)
    result = merge(parts, a.scene, a.round)
    (folder / "review.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    (folder / "review.md").write_text(markdown(result), encoding="utf-8")
    sev = {}
    for i in result["issues"]:
        sev[i.get("severity")] = sev.get(i.get("severity"), 0) + 1
    print("REVIEW", a.scene, "round", a.round, result["approval_decision"], sev, "->", folder / "review.md")


if __name__ == "__main__":
    main()

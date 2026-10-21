"""Prints a batch of frozen specifications for authoring: id, act, situation, form, recipient, style, persona, length and the content seed. No label is shown beyond the spec fields."""
import json
import sys
from pathlib import Path

specs = json.loads((Path(__file__).resolve().parents[2] / "tests" / "synthetic_dev_v1" / "specs.json").read_text(encoding="utf-8"))
lo, hi = int(sys.argv[1]), int(sys.argv[2])
for s in specs[lo - 1:hi]:
    a = "" if not s["acts"] else " ACTS=" + "+".join("%s:%s" % (x["act"], x["situation"]) for x in s["acts"])
    pr = (" | PRIOR(%s): %s" % (s["prior_speaker"], s["prior_turn"])) if s["prior_turn"] else ""
    print("%s %s %s/%s form=%s to=%s style=%s %s len=%s%s%s | %s%s" % (s["id"][4:], s["objective"], s["situation"], s["cls"], s["form"], s["recipient"], s["style"], s["persona"], s["length"], " HEDGE" if s["hedge"] else "", a, s["seed"], pr))

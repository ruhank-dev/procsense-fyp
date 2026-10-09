"""Writes content/rules/vectors.json: test vectors for the rule evaluator (p0-clinical-logic-s01).

Each vector is { name, rule, data, expect }; each error vector is { name, rule, error: "compile" | "runtime" }.
Expected answers come from Python's own operators or are stated by hand, never from the C# code under test, so the
vectors can catch a real mistake. Run again after editing:  uv run --project tools tools/rules/make_vectors.py
"""
import json
import operator
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "content" / "rules" / "vectors.json"
vectors: list[dict] = []
errors: list[dict] = []


def v(name, rule, data, expect):
    vectors.append({"name": name, "rule": rule, "data": data, "expect": expect})


def err(name, rule, kind):
    errors.append({"name": name, "rule": rule, "error": kind})


def var(name):
    return {"var": name}


# --- ordering and equality over number pairs (expected from Python's operators)
OPS = {"<": operator.lt, "<=": operator.le, ">": operator.gt, ">=": operator.ge, "==": operator.eq, "!=": operator.ne}
PAIRS = [(1, 2), (2, 1), (2, 2), (-1, 1), (0, 0), (0.5, 0.25), (98, 94), (36.7, 38)]
for sym, fn in OPS.items():
    for a, b in PAIRS:
        v(f"{a} {sym} {b}", {sym: [var("a"), var("b")]}, {"a": a, "b": b}, fn(a, b))

# --- a missing variable is null: ordering with null is false, null equals only null
for sym in ("<", "<=", ">", ">="):
    v(f"missing {sym} 3 is false", {sym: [var("nope"), 3]}, {}, False)
v("null == null", {"==": [var("a"), var("b")]}, {}, True)
v("null == 0 is false", {"==": [var("a"), 0]}, {}, False)
v("null != 3", {"!=": [var("a"), 3]}, {}, True)
v("null < null is false", {"<": [var("a"), var("b")]}, {}, False)

# --- strict equality: no coercion
for name, a, b, expect in [
    ("1 == '1'", 1, "1", False), ("'1' == 1", "1", 1, False), ("true == 1", True, 1, False), ("0 == false", 0, False, False),
    ("'' == false", "", False, False), ("null == false", None, False, False), ("'a' == 'a'", "a", "a", True),
    ("'a' == 'A'", "a", "A", False), ("[1,2] == [1,2]", [1, 2], [1, 2], True), ("[1,2] == [2,1]", [1, 2], [2, 1], False),
    ("1 == 1.0", 1, 1.0, True), ("'' == ''", "", "", True),
]:
    v("strict: " + name, {"==": [{"var": "a"}, {"var": "b"}]}, {"a": a, "b": b}, expect)

# --- text ordering is ordinal
v("'a' < 'b'", {"<": ["a", "b"]}, {}, True)
v("'b' < 'a' is false", {"<": ["b", "a"]}, {}, False)

# --- truthiness through ! and if
TRUTHY = [(0, False), (1, True), ("", False), ("x", True), ([], False), ([0], True), (None, False), (False, False), (True, True), (2.5, True), (-1, True)]
for value, truthy in TRUTHY:
    v(f"! {json.dumps(value)}", {"!": [var("v")]}, {"v": value}, not truthy)
    v(f"if {json.dumps(value)}", {"if": [var("v"), "yes", "no"]}, {"v": value}, "yes" if truthy else "no")

# --- and / or return the deciding value
v("and 1 2", {"and": [1, 2]}, {}, 2)
v("and 1 0 3", {"and": [1, 0, 3]}, {}, 0)
v("and true 'x'", {"and": [True, "x"]}, {}, "x")
v("and null 1", {"and": [None, 1]}, {}, None)
v("or 0 '' 5", {"or": [0, "", 5]}, {}, 5)
v("or 0 ''", {"or": [0, ""]}, {}, "")
v("or false 'a'", {"or": [False, "a"]}, {}, "a")
v("or null null", {"or": [None, None]}, {}, None)

# --- in
v("2 in [1,2,3]", {"in": [2, [1, 2, 3]]}, {}, True)
v("4 in [1,2,3]", {"in": [4, [1, 2, 3]]}, {}, False)
v("'a' in ['a']", {"in": ["a", ["a"]]}, {}, True)
v("1 in ['1'] is false", {"in": [1, ["1"]]}, {}, False)
v("'ell' in 'hello'", {"in": ["ell", "hello"]}, {}, True)
v("'z' in 'hello'", {"in": ["z", "hello"]}, {}, False)
v("null in [null]", {"in": [None, [None]]}, {}, True)
v("in a missing list is false", {"in": [1, var("nope")]}, {}, False)

# --- var
v("var plain", var("a"), {"a": 5}, 5)
v("var dotted", var("a.b"), {"a": {"b": 6}}, 6)
v("var missing deep", var("a.b.c"), {"a": {"b": 1}}, None)
v("var default used", {"var": ["nope", 7]}, {}, 7)
v("var default not used", {"var": ["a", 7]}, {"a": 1}, 1)
v("var list index", var("xs.1"), {"xs": [10, 20, 30]}, 20)
v("var list index out of range", var("xs.9"), {"xs": [10]}, None)
v("var through a scalar is missing", var("a.b"), {"a": 3}, None)

# --- missing
v("missing one", {"missing": ["a", "b"]}, {"a": 1}, ["b"])
v("missing all", {"missing": ["a", "b"]}, {}, ["a", "b"])
v("missing none", {"missing": ["a", "b"]}, {"a": 1, "b": 2}, [])
v("missing nested", {"missing": ["x.y"]}, {"x": {"z": 1}}, ["x.y"])
v("a null value counts as missing", {"missing": ["a"]}, {"a": None}, ["a"])

# --- some / all (the test runs with each item as the data; empty lists give false for both)
GT2 = {">": [var(""), 2]}
v("some > 2 true", {"some": [var("xs"), GT2]}, {"xs": [1, 2, 3]}, True)
v("some > 2 false", {"some": [var("xs"), GT2]}, {"xs": [1, 2]}, False)
v("some of empty", {"some": [var("xs"), GT2]}, {"xs": []}, False)
v("some of missing", {"some": [var("xs"), GT2]}, {}, False)
v("all > 2 true", {"all": [var("xs"), GT2]}, {"xs": [3, 4]}, True)
v("all > 2 false", {"all": [var("xs"), GT2]}, {"xs": [3, 1]}, False)
v("all of empty", {"all": [var("xs"), GT2]}, {"xs": []}, False)
v("all of missing", {"all": [var("xs"), GT2]}, {}, False)
v("some over objects", {"some": [var("xs"), {"==": [var("k"), "x"]}]}, {"xs": [{"k": "y"}, {"k": "x"}]}, True)
v("all over objects", {"all": [var("xs"), {"==": [var("k"), "x"]}]}, {"xs": [{"k": "x"}, {"k": "y"}]}, False)
v("all containing some", {"all": [var("rows"), {"some": [var("cells"), {"==": [var(""), 1]}]}]}, {"rows": [{"cells": [1, 2]}, {"cells": [3, 1]}]}, True)
v("some containing all", {"some": [var("rows"), {"all": [var("cells"), {">": [var(""), 5]}]}]}, {"rows": [{"cells": [1, 9]}, {"cells": [6, 7]}]}, True)

# --- arithmetic
v("+ three", {"+": [1, 2, 3]}, {}, 6)
v("+ decimals", {"+": [1.5, 2.5]}, {}, 4)
v("+ null is null", {"+": [1, var("nope")]}, {}, None)
v("- two", {"-": [5, 3]}, {}, 2)
v("- one negates", {"-": [5]}, {}, -5)
v("- null is null", {"-": [var("nope"), 3]}, {}, None)
v("+ vars", {"+": [var("a"), var("b")]}, {"a": 40, "b": 2}, 42)
v("nested arithmetic", {"-": [{"+": [10, 5]}, {"+": [1, 2]}]}, {}, 12)

# --- between (three arguments)
v("1 < 2 < 3", {"<": [1, 2, 3]}, {}, True)
v("1 < 3 < 3 is false", {"<": [1, 3, 3]}, {}, False)
v("1 <= 3 <= 3", {"<=": [1, 3, 3]}, {}, True)
v("3 < 2 < 1 is false", {"<": [3, 2, 1]}, {}, False)
v("a value inside a range", {"<=": [94, var("x"), 96]}, {"x": 95}, True)

# --- if chains
v("if picks the second branch", {"if": [False, "a", True, "b", "c"]}, {}, "b")
v("if falls to else", {"if": [False, "a", False, "b", "c"]}, {}, "c")
v("if without else gives null", {"if": [False, "a"]}, {}, None)
v("if with one argument", {"if": ["x"]}, {}, "x")

# --- combined, rule-shaped
RANGE = {"and": [{">=": [var("n"), 1]}, {"<": [var("n"), 5]}]}
v("range inside", RANGE, {"n": 3}, True)
v("range at the open end", RANGE, {"n": 5}, False)
v("range with a missing value", RANGE, {}, False)
v("or of two conditions", {"or": [{"==": [var("a"), "x"]}, {"in": [var("b"), ["p", "q"]]}]}, {"a": "y", "b": "q"}, True)
v("not and", {"!": [{"and": [True, {"==": [var("a"), 1]}]}]}, {"a": 2}, True)

# --- depth: 31 nested operations over a literal is allowed (32 levels), 32 is not
deep_ok: object = True
for _ in range(31):
    deep_ok = {"!": [deep_ok]}
v("31 nested operations are allowed", deep_ok, {}, False)   # "not" applied an odd number of times (31) flips true to false
deep_bad: object = True
for _ in range(32):
    deep_bad = {"!": [deep_bad]}
err("32 nested operations are too deep", deep_bad, "compile")

# --- errors: unknown or malformed rules fail when loaded, wrong types fail when run
err("unknown operator", {"foo": [1, 2]}, "compile")
err("two operators in one object", {"==": [1, 1], "!=": [1, 2]}, "compile")
err("== needs two arguments", {"==": [1]}, "compile")
err("! needs one argument", {"!": [1, 2]}, "compile")
err("var needs a name", {"var": []}, "compile")
err("object value that is not an operation", {}, "compile")
err("number < text", {"<": [3, "a"]}, "runtime")
err("true < false", {"<": [True, False]}, "runtime")
err("list < list", {"<": [[1], [2]]}, "runtime")
err("text + number", {"+": ["a", 1]}, "runtime")
err("some over a number", {"some": [3, {"==": [1, 1]}]}, "runtime")
err("var with a true/false name", {"var": [True]}, "runtime")
err("number in text", {"in": [1, "abc"]}, "runtime")

assert len(vectors) >= 150, len(vectors)
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(json.dumps({"vectors": vectors, "errors": errors}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"wrote {OUT}: {len(vectors)} vectors, {len(errors)} error vectors")

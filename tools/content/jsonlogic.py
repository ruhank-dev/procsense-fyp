"""Python twin of the rule evaluator (p0-clinical-logic-s02, experiment EXP-CL-3). Same JsonLogic subset and the same strict rules as src/ProcSense.Core/Rules/Logic.cs; no eval().

Operators: var, ==, !=, <, <=, >, >=, and, or, !, in, if, missing, some, all, +, -.
Strict types (1 == "1" is false, true == 1 is false), a missing variable is null, comparing null is false, text ordering is ordinal, truthy means not null/false/0/""/[].
The vectors in content/rules/vectors.json are the contract: fix a meaning in both implementations or in neither.
"""
from __future__ import annotations

MAX_DEPTH = 32
OPERATORS = ("var", "==", "!=", "<", "<=", ">", ">=", "and", "or", "!", "in", "if", "missing", "some", "all", "+", "-")


class RuleError(Exception):
    """The rule itself is wrong (kind 'compile': unknown operator, wrong argument count, too deep) or met data of the wrong type (kind 'runtime')."""

    def __init__(self, kind: str, message: str) -> None:
        super().__init__(message)
        self.kind = kind


def _arity(op: str) -> tuple[int, int]:
    if op == "var":
        return 1, 2
    if op in ("==", "!=", ">", ">=", "in", "some", "all"):
        return 2, 2
    if op in ("<", "<="):
        return 2, 3
    if op == "!":
        return 1, 1
    if op == "-":
        return 1, 2
    return 1, 1 << 30


def compile_rule(rule, path: str = "rule", depth: int = 1) -> None:
    """Raises RuleError('compile') for anything outside the subset."""
    if depth > MAX_DEPTH:
        raise RuleError("compile", f"{path}: rule is nested more than {MAX_DEPTH} levels deep")
    if rule is None or isinstance(rule, (bool, int, float, str)):
        return
    if isinstance(rule, list):
        for i, x in enumerate(rule):
            compile_rule(x, f"{path}[{i}]", depth + 1)
        return
    if not isinstance(rule, dict):
        raise RuleError("compile", f"{path}: unsupported value of type {type(rule).__name__}")
    if len(rule) != 1:
        raise RuleError("compile", f"{path}: an operation must have exactly one operator, found {len(rule)}")
    op, raw = next(iter(rule.items()))
    if op not in OPERATORS:
        raise RuleError("compile", f"{path}: unknown operator '{op}' (allowed: {', '.join(sorted(OPERATORS))})")
    args = raw if isinstance(raw, list) else [raw]
    lo, hi = _arity(op)
    if not lo <= len(args) <= hi:
        raise RuleError("compile", f"{path}: '{op}' takes {lo}{'' if lo == hi else (' or more' if hi > 100 else f' to {hi}')} argument(s), found {len(args)}")
    for i, x in enumerate(args):
        compile_rule(x, f"{path}.{op}[{i}]", depth + 1)


def reads(rule, keys: set[str]) -> bool:
    """Collects the top-level data keys a rule reads into `keys`. False when a read cannot be named in advance (a computed name or the whole data)."""
    if isinstance(rule, list):
        return all(reads(x, keys) for x in rule)
    if not isinstance(rule, dict) or len(rule) != 1:
        return True
    op, raw = next(iter(rule.items()))
    args = raw if isinstance(raw, list) else [raw]
    if op == "var":
        if not args or not isinstance(args[0], str) or args[0] == "":
            return False
        keys.add(args[0].split(".")[0])
        return all(reads(x, keys) for x in args[1:])
    if op == "missing":
        for a in args:
            for n in (a if isinstance(a, list) else [a]):
                if not isinstance(n, str) or n == "":
                    return False
                keys.add(n.split(".")[0])
        return True
    if op in ("some", "all"):
        return not args or reads(args[0], keys)
    return all(reads(x, keys) for x in args)


def truthy(v) -> bool:
    if v is None:
        return False
    if isinstance(v, bool):
        return v
    if isinstance(v, (int, float)):
        return v != 0
    if isinstance(v, (str, list)):
        return len(v) != 0
    return True


def _is_num(v) -> bool:
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _same(a, b) -> bool:
    if a is None or b is None:
        return a is None and b is None
    if _is_num(a) and _is_num(b):
        return a == b
    if isinstance(a, bool) and isinstance(b, bool):
        return a == b
    if isinstance(a, str) and isinstance(b, str):
        return a == b
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(_same(x, y) for x, y in zip(a, b))
    return False


def _describe(v) -> str:
    if v is None:
        return "nothing (null)"
    if isinstance(v, bool):
        return "true/false"
    if _is_num(v):
        return "a number"
    if isinstance(v, str):
        return "text"
    if isinstance(v, list):
        return "a list"
    return "an object"


def _compare(a, b):
    """None = cannot be ordered (a missing value); otherwise the sign of a - b. Mixed or unordered types are an error."""
    if a is None or b is None:
        return None
    if (_is_num(a) and _is_num(b)) or (isinstance(a, str) and isinstance(b, str)):
        return (a > b) - (a < b)
    raise RuleError("runtime", f"cannot order {_describe(a)} against {_describe(b)}")


def _lookup(data, name: str):
    if name == "":
        return data
    cur = data
    for part in name.split("."):
        if isinstance(cur, dict):
            if part not in cur:
                return None
            cur = cur[part]
        elif isinstance(cur, list) and part.isdigit() and int(part) < len(cur):
            cur = cur[int(part)]
        else:
            return None
    return cur


def _name(v) -> str:
    if isinstance(v, str):
        return v
    if _is_num(v):
        return str(int(v)) if float(v).is_integer() else repr(float(v))
    raise RuleError("runtime", f"a variable name must be text, not {_describe(v)}")


def _number(v, op: str):
    if _is_num(v):
        return v
    raise RuleError("runtime", f"'{op}' needs numbers, not {_describe(v)}")


def evaluate(rule, data=None):
    """The raw result of a rule (number, text, true/false, list or null). A mistake in the rule raises RuleError('runtime')."""
    if isinstance(rule, list):
        return [evaluate(x, data) for x in rule]
    if not isinstance(rule, dict):
        return rule
    op, raw = next(iter(rule.items()))
    args = raw if isinstance(raw, list) else [raw]
    if op == "var":
        name = evaluate(args[0], data)
        if not (isinstance(name, str) or _is_num(name)):
            raise RuleError("runtime", f"var needs a name (text), not {_describe(name)}")
        found = _lookup(data, _name(name))
        return found if found is not None else (evaluate(args[1], data) if len(args) == 2 else None)
    if op == "==":
        return _same(evaluate(args[0], data), evaluate(args[1], data))
    if op == "!=":
        return not _same(evaluate(args[0], data), evaluate(args[1], data))
    if op in ("<", "<=", ">", ">="):
        vals = [evaluate(a, data) for a in args]
        for i in range(len(vals) - 1):
            c = _compare(vals[i], vals[i + 1])
            if c is None:
                return False
            c = c if op in ("<", "<=") else -c
            if (c >= 0) if op in ("<", ">") else (c > 0):
                return False
        return True
    if op in ("and", "or"):
        last = None
        for a in args:
            last = evaluate(a, data)
            if truthy(last) == (op == "or"):
                return last
        return last
    if op == "!":
        return not truthy(evaluate(args[0], data))
    if op == "in":
        needle, hay = evaluate(args[0], data), evaluate(args[1], data)
        if hay is None:
            return False
        if isinstance(hay, list):
            return any(_same(needle, x) for x in hay)
        if isinstance(hay, str):
            if isinstance(needle, str):
                return needle in hay
            if needle is None:
                return False
            raise RuleError("runtime", f"'in' on text needs text to look for, not {_describe(needle)}")
        raise RuleError("runtime", f"'in' needs a list or text to search, not {_describe(hay)}")
    if op == "if":
        n = len(args)
        for i in range(0, n - 1, 2):
            if truthy(evaluate(args[i], data)):
                return evaluate(args[i + 1], data)
        return evaluate(args[-1], data) if n % 2 == 1 else None
    if op == "missing":
        names: list[str] = []
        for a in args:
            v = evaluate(a, data)
            for x in (v if isinstance(v, list) else [v]):
                names.append(_name(x))
        return [n for n in names if _lookup(data, n) is None]
    if op in ("some", "all"):
        source = evaluate(args[0], data)
        if source is None:
            return False
        if not isinstance(source, list):
            raise RuleError("runtime", f"{op} needs a list, not {_describe(source)}")
        if not source:
            return False
        for item in source:
            ok = truthy(evaluate(args[1], item))
            if op == "all" and not ok:
                return False
            if op == "some" and ok:
                return True
        return op == "all"
    if op == "+":
        total = 0
        for a in args:
            v = evaluate(a, data)
            if v is None:
                return None
            total += _number(v, "+")
        return total
    a = evaluate(args[0], data)   # "-"
    if a is None:
        return None
    if len(args) == 1:
        return -_number(a, "-")
    b = evaluate(args[1], data)
    if b is None:
        return None
    return _number(a, "-") - _number(b, "-")

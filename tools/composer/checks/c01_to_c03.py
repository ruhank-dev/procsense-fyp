"""Checks 1 to 3: schema, required fields, constraints (the rules of tools/content/case_file_checks.py, sorted into Stage B codes)."""
from __future__ import annotations

from . import Defect
from .base import defects


def c01_schema(card: dict, ctx: dict) -> list[Defect]:
    return defects(card, ctx, 1)


def c02_required(card: dict, ctx: dict) -> list[Defect]:
    return defects(card, ctx, 2) if not defects(card, ctx, 1) else []


def c03_constraints(card: dict, ctx: dict) -> list[Defect]:
    return defects(card, ctx, 3) if not defects(card, ctx, 1) else []

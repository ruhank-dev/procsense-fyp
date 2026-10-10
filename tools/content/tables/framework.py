"""Builds content tables and their source-register rows from one definition, and checks every row against the book.

A table module defines rows in Python (data only). `build()` then
  1. verifies each row: its `quote` is on its `page` of the walkthrough, and every value in a `v_` column is inside that quote;
  (columns: v_ = value that must be in the book quote; o_ = value that must be in the official quote; d_ = derived, e.g. "hourly" -> 60, not checked, needs a note)
  2. if the row has an `official` quote (text from the guideline itself, kept in docs/research/official_quotes/), verifies that too;
  3. writes the CSV under content/ (columns: id, the data columns, quote, page, ref);
  4. writes the register rows (SR ids from the table's reserved block; status VERIFIED only when the official source was checked).
Rows with ids in a reserved block belong to the generator and are rewritten on every run; hand-written rows (below SR-0100) are never touched.
"""
from __future__ import annotations

import csv
import io
import re
from dataclasses import dataclass, field
from pathlib import Path

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import book  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
CONTENT = ROOT / "content"
REGISTER = ROOT / "docs" / "research" / "source_register.csv"
OFFICIAL = ROOT / "docs" / "research" / "official_quotes"
REGISTER_HEADER = ["id", "item", "value", "unit", "walkthrough_ref", "source_name", "source_location", "source_url", "evidence_type", "status",
                   "date_checked", "checked_by", "clinician_approval", "notes"]
OWNER_APPROVAL = "Approved (existing research and clinical workflows approved by the project owner 09 Oct 2026)"
TODAY = "2026-10-10"
NUMBER = re.compile(r"\d+(?:\.\d+)?")
FRAGMENT_SEPARATOR = " ‖ "   # a quote made of several pieces, each of which must be on the cited pages


class BuildError(Exception):
    pass


def _tokens(text: str) -> set[str]:
    return {t.rstrip("0").rstrip(".") if "." in t else t for t in NUMBER.findall(text)}


def official_text(name: str) -> str:
    return book.normalise((OFFICIAL / name).read_text(encoding="utf-8"))


@dataclass
class Table:
    name: str                      # content/<name>.csv
    block: int                     # first register id of the block this table owns, e.g. 100 -> SR-0100..SR-0199
    columns: list[str]             # data columns; those starting with v_ are checked against the quote
    describe: str                  # what the register row for one row says, e.g. "Vital band"
    source_location: str           # where in the book, e.g. "Part 17 table 'Vital signs (adults, not pregnant)'"
    size: int = 100                # how many register ids the block holds
    evidence: str = "A"            # A source-backed, B interpretation, C institutional, D simulator design value
    official_name: str = ""        # official source, e.g. "RCP National Early Warning Score 2 (2017)"
    official_url: str = ""
    rows: list[dict] = field(default_factory=list)

    def add(self, key: str, quote: str, page: int | str, *, official: str = "", official_page: str = "", evidence: str = "", note: str = "", **data) -> None:
        if page == "span":   # long text that may cross page breaks: first page of the first fragment to last page of the last fragment
            frags = [book.normalise(f) for f in quote.split(FRAGMENT_SEPARATOR) if f.strip()]
            def locate(text: str, head: bool) -> list[int]:
                # a probe that straddles a page break is on no single page: try shorter probes
                for size in (70, 40, 25, 15):
                    found = book.find_page(text[:size] if head else text[-size:])
                    if found:
                        return found
                return []
            start = locate(frags[0], True) if frags else []
            end = locate(frags[-1], False) if frags else []
            if not start or not end:
                raise BuildError(f"{self.name}/{key}: could not place the quote on a page: {quote[:60]!r}")
            page = f"{start[0]}-{end[-1]}" if end[-1] != start[0] else str(start[0])
        if page == "auto":   # the one page the quote is on (a quote on two pages is an error: say which)
            found = book.find_page(quote) if quote else []
            if len(found) != 1:
                raise BuildError(f"{self.name}/{key}: quote is on pages {found}; give the page explicitly: {quote[:60]!r}")
            page = found[0]
        self.rows.append({"key": key, "quote": quote, "page": str(page), "official": official, "official_page": official_page,
                          "evidence": evidence or self.evidence, "note": note, **data})

    def add_derived(self, key: str, dataset: str, url: str, method: str, **data) -> None:
        """A row computed from an external dataset by a script (no walkthrough quote). The register row names the dataset, its licence page and the method;
        clinician approval stays pending until the plots are reviewed (EXP-DATA-1)."""
        self.rows.append({"key": key, "quote": "", "page": "", "official": "", "official_page": "", "evidence": "B", "note": method,
                          "derived_from": dataset, "derived_url": url, **data})

    # ---------------------------------------------------------------- checks

    def check(self, official_file: str = "") -> list[str]:
        problems = []
        official = official_text(official_file) if official_file and (OFFICIAL / official_file).exists() else ""
        for r in self.rows:
            where = f"{self.name}/{r['key']}"
            fragments = [book.normalise(f) for f in r["quote"].split(FRAGMENT_SEPARATOR) if f.strip()]
            quote = " ".join(fragments)
            if r["quote"]:
                first, _, last = r["page"].partition("-")
                text = book.page_text(int(first), int(last or first))
                gone = [f for f in fragments if f not in text]
                if gone:
                    problems.append(f"{where}: quote not found on page {r['page']}: {gone[0][:70]!r}")
                    continue
            elif r.get("derived_from"):
                if not r["note"]:
                    problems.append(f"{where}: a derived row needs a method note")
                continue
            elif not r["official"] or not r["note"]:
                problems.append(f"{where}: a row with no walkthrough quote needs an official quote and a note saying why")
                continue
            for col in self.columns:
                if not col.startswith("v_") or not r["quote"]:
                    continue
                value = str(r.get(col, "")).strip()
                if not value:
                    continue
                numbers = _tokens(value)
                missing = numbers - _tokens(quote)
                if missing:
                    problems.append(f"{where}: {col}={value!r} has numbers not in the quote: {sorted(missing)}")
                if not numbers and value.lower() not in quote.lower():
                    problems.append(f"{where}: {col}={value!r} is not in the quote")
            if r["official"]:
                for col in self.columns:   # o_ columns exist only in the official source, so they are checked against it alone
                    value = str(r.get(col, "")).strip()
                    if col.startswith("o_") and value:
                        numbers = _tokens(value)
                        if numbers - _tokens(r["official"]) or (not numbers and value.lower() not in book.normalise(r["official"]).lower()):
                            problems.append(f"{where}: {col}={value!r} is not in the official quote")
                if not official:
                    problems.append(f"{where}: official quote given but no official file loaded")
                elif book.normalise(r["official"]) not in official:
                    problems.append(f"{where}: official quote not in the official source: {r['official'][:70]!r}")
                else:
                    miss = {n for col in self.columns if col.startswith(("v_", "o_")) for n in _tokens(str(r.get(col, "")))} - _tokens(r["official"])
                    if miss:
                        problems.append(f"{where}: values {sorted(miss)} are in the book but not in the official quote")
        keys = [r["key"] for r in self.rows]
        problems += [f"{self.name}: duplicate key {k}" for k in sorted({k for k in keys if keys.count(k) > 1})]
        if len(self.rows) > self.size:
            problems.append(f"{self.name}: {len(self.rows)} rows do not fit the reserved block of {self.size} register ids")
        return problems

    # ---------------------------------------------------------------- outputs

    def register_rows(self) -> list[list[str]]:
        out = []
        for i, r in enumerate(self.rows):
            rid = f"SR-{self.block + i:04d}"
            r["ref"] = rid
            if r.get("derived_from"):
                out.append([rid, f"{self.describe}: {r['key']}", self.value_text(r), "", "", r["derived_from"], self.source_location, r["derived_url"], "B", "VERIFIED", TODAY,
                            "Project team (script)", "Pending: clinician review of the plots (EXP-DATA-1)",
                            "Computed from the dataset by a reproducible script (see the dataset register and card). " + r["note"]])
                continue
            checked = bool(r["official"])
            value = self.value_text(r)
            src = f"{self.official_name}; ProcSense Medical Walkthrough v5.1" if checked else "ProcSense Medical Walkthrough v5.1"
            status = "VERIFIED" if checked else ("VERIFIED" if r["evidence"] == "D" else "UNVERIFIED")
            notes = []
            if checked:
                notes.append(f"Original source opened and matches ({r['official_page'] or 'see source'}); also matches the walkthrough page verbatim.")
            elif r["evidence"] == "D":
                notes.append("Simulator design value taken from the walkthrough; matches the page verbatim.")
            else:
                notes.append("Matches the walkthrough page verbatim; the original guideline has not been opened yet.")
            if r["note"]:
                notes.append(r["note"])
            out.append([rid, f"{self.describe}: {r['key']}", value, "", f"WT {self.source_location}, p.{r['page']}", src, self.source_location,
                        self.official_url if checked else "", r["evidence"], status, TODAY, "Project team", OWNER_APPROVAL, " ".join(notes)])
        return out

    @staticmethod
    def _interval(low, high, bound="") -> str:
        low, high = str(low).strip(), str(high).strip()
        if low and high:
            return f"{low}–{high}"
        if low:
            return f">{low}" if bound == "above" else f"≥{low}"
        if high:
            return f"<{high}" if bound == "below" else f"≤{high}"
        return ""

    def value_text(self, r: dict) -> str:
        """A readable value for the register: '51–90', '≥131', plus any other checked columns as name=value."""
        parts = []
        first = self._interval(r.get("v_low", ""), r.get("v_high", ""), r.get("bound", ""))
        second = self._interval(r.get("v_low2", ""), r.get("v_high2", ""), r.get("bound", ""))
        if first:
            parts.append(first)
        if second:
            parts.append(f"/ {second}")
        skip = {"v_low", "v_high", "v_low2", "v_high2"}
        for c in self.columns:
            if c.startswith(("v_", "o_", "d_")) and c not in skip and str(r.get(c, "")).strip():
                parts.append(f"{c[2:]}={r[c]}")
        return " ".join(parts) or r["quote"][:60] or r["key"]

    def csv_text(self) -> str:
        self.register_rows()   # fills r["ref"]
        buf = io.StringIO()
        w = csv.writer(buf, lineterminator="\n")
        w.writerow(["id", *self.columns, "quote", "page", "ref"])
        for r in self.rows:
            w.writerow([r["key"], *[r.get(c, "") for c in self.columns], FRAGMENT_SEPARATOR.join(book.normalise(f) for f in r["quote"].split(FRAGMENT_SEPARATOR)), r["page"], r["ref"]])
        return buf.getvalue()

    def write_csv(self) -> None:
        (CONTENT / f"{self.name}.csv").write_text(self.csv_text(), encoding="utf-8", newline="")


def merged_register(tables: list[Table]) -> str:
    """The register text with the generated blocks replaced by the tables' rows (every other row is kept as it is)."""
    blocks = [(t.block, t.block + t.size - 1) for t in tables]
    with open(REGISTER, encoding="utf-8", newline="") as f:
        reader = list(csv.reader(f))
    header, rows = reader[0], reader[1:]
    if header != REGISTER_HEADER:
        raise BuildError(f"register header changed: {header}")
    keep = [r for r in rows if not any(lo <= int(r[0][3:]) <= hi for lo, hi in blocks)]
    new = [row for t in tables for row in t.register_rows()]
    ids = [r[0] for r in keep + new]
    dupes = {i for i in ids if ids.count(i) > 1}
    if dupes:
        raise BuildError(f"register id clash: {sorted(dupes)}")
    merged = sorted(keep + new, key=lambda r: r[0])
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    w.writerows(merged)
    return buf.getvalue()


def rewrite_register(tables: list[Table]) -> None:
    REGISTER.write_text(merged_register(tables), encoding="utf-8", newline="")

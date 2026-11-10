#!/usr/bin/env python3
"""Fails when two diagnostic presets share an id (PerfPresets.cs): a duplicate id silently makes the benchmark pick the first one. Usage: python tools/unity/check_preset_ids.py"""
import re, sys, collections
src = open("unity/Assets/Scripts/Diagnostics/PerfPresets.cs", encoding="utf-8").read()
ids = re.findall(r'new PerfPreset \{ Id = "(B\d+)"', src)
dups = sorted(k for k, v in collections.Counter(ids).items() if v > 1)
print(f"{len(ids)} presets, {len(set(ids))} unique ids" + (f"; DUPLICATES: {', '.join(dups)}" if dups else ""))
sys.exit(1 if dups else 0)

# loc_en.py - extract English localisation strings from gamedata/resources.assets.
#
# Usage (from repo root):  .venv/bin/python tools/loc_en.py [key-substring ...]
# Writes gamedata/loc_en.json (key -> English) and prints matching keys if arguments are given.
# Keys are stored without the leading '$' (the game strips it when looking up tokens).
import csv
import io
import json
import os
import sys

import UnityPy

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load():
    env = UnityPy.load(os.path.join(ROOT, "gamedata/resources.assets"))
    out = {}
    for o in env.objects:
        if o.type.name != "TextAsset":
            continue
        d = o.read()
        if not (d.m_Name.startswith("localization") or d.m_Name == "heightmap_message"):
            continue
        s = d.m_Script if isinstance(d.m_Script, str) else d.m_Script.decode("utf-8", "replace")
        s = s.lstrip("﻿")
        rows = csv.reader(io.StringIO(s))
        header = next(rows)
        try:
            col = header.index("English")
        except ValueError:
            continue
        for r in rows:
            if len(r) > col and r[0]:
                out.setdefault(r[0], r[col])
    return out


def main():
    loc = load()
    with open(os.path.join(ROOT, "gamedata/loc_en.json"), "w") as f:
        json.dump(loc, f, indent=0, ensure_ascii=False)
    for arg in sys.argv[1:]:
        for k, v in sorted(loc.items()):
            if arg.lower() in k.lower():
                print("%s = %s" % (k, v.replace("\n", "\\n")[:200]))
    print(len(loc), "keys")


if __name__ == "__main__":
    main()

# find_refs.py - reverse-reference search: which MonoBehaviours / AnimationClips / other objects in the
# SoftRef bundles point (PPtr, same file) at the named GameObject prefabs.
#
# Usage (from repo root):  .venv/bin/python tools/find_refs.py sfx_goblinking_beam sfx_gdking_scream ...
# Prints, per name: referencing object type, script/clip name and owning GameObject.
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vhassets import Assets  # noqa: E402


def main():
    names = sys.argv[1:]
    A = Assets()
    targets = {}
    for n in names:
        for o in A.go_by_name.get(n, []):
            targets.setdefault((id(o.assets_file), o.path_id), n)
    if not targets:
        print("no such GameObjects")
        return
    pats = {}
    for (fid, pid), n in targets.items():
        pats.setdefault(fid, []).append((struct.pack("<iq", 0, pid), n, pid))
    hits = {n: [] for n in names}
    for sf in A.cabs.values():
        plist = pats.get(id(sf))
        if not plist:
            continue
        for pid, o in sf.objects.items():
            if o.type.name not in ("MonoBehaviour", "AnimationClip"):
                continue
            raw = o.get_raw_data()
            for pat, n, tpid in plist:
                if pat in raw:
                    if o.type.name == "AnimationClip":
                        hits[n].append("AnimationClip '%s'" % o.peek_name())
                    else:
                        hits[n].append("%s on %s" % (A.script_name(o), A.name_of(o).split("(")[0]))
    for n in names:
        print("%s: %s" % (n, "; ".join(sorted(set(hits[n]))) or ("NOT REFERENCED" if any(v == n for v in targets.values()) else "no such prefab")))


if __name__ == "__main__":
    main()

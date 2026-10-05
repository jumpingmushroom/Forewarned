# vhassets.py - shared helpers for reading Valheim SoftRef asset bundles with UnityPy.
#
# Usage (from repo root):  from tools.vhassets import Assets   (or sys.path insert tools/)
#
# Needs: .venv with UnityPy + TypeTreeGeneratorAPI (pip install TypeTreeGeneratorAPI),
#        gamedata/Managed/*.dll (copy of valheim_Data/Managed from the game install),
#        gamedata/StreamingAssets/SoftRef/Bundles/c4210710 (prefabs) and 86c3d76e (MonoScripts).
#
# MonoBehaviours are decoded through TypeTreeGeneratorAPI, which builds Unity type trees from the
# game's managed DLLs (player bundles ship without type trees). read_typetree(check_read=True)
# raises if the decoded tree does not consume exactly the object's raw byte length, so every
# decoded MonoBehaviour is length-verified.
import os
import glob
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUNDLES = os.path.join(ROOT, "gamedata/StreamingAssets/SoftRef/Bundles")
MANAGED = os.path.join(ROOT, "gamedata/Managed")


class Assets:
    def __init__(self, bundles=None):
        if bundles is None:
            bundles = sorted(glob.glob(os.path.join(BUNDLES, "*")))
        self.env = UnityPy.load(*bundles)
        # map CAB name -> SerializedFile
        self.cabs = {}
        for bf in self.env.files.values():
            for name, sf in getattr(bf, "files", {}).items():
                if hasattr(sf, "objects"):
                    self.cabs[name.lower()] = sf
        ver = next(iter(self.cabs.values())).unity_version
        gen = TypeTreeGenerator(ver)
        gen.load_local_dll_folder(MANAGED)
        self.env.typetree_generator = gen
        self.go_by_name = {}
        self._script_cache = {}
        self._tt_cache = {}
        for sf in self.cabs.values():
            for pid, o in sf.objects.items():
                if o.type.name == "GameObject":
                    try:
                        n = o.peek_name()
                    except Exception:
                        n = o.read().m_Name
                    self.go_by_name.setdefault(n, []).append(o)

    # ---- pointers -------------------------------------------------------------
    def deref(self, pptr, src):
        """pptr: dict {'m_FileID','m_PathID'} or PPtr; src: ObjectReader that owns the pointer."""
        if isinstance(pptr, dict):
            fid, pid = pptr["m_FileID"], pptr["m_PathID"]
        else:
            fid, pid = pptr.m_FileID, pptr.m_PathID
        if pid == 0:
            return None
        sf = src.assets_file
        if fid != 0:
            ext = sf.externals[fid - 1].path
            cab = os.path.basename(ext).lower()
            sf = self.cabs.get(cab)
            if sf is None:
                return ("external", ext, pid)
        return sf.objects.get(pid)

    def name_of(self, obj):
        if obj is None:
            return None
        if isinstance(obj, tuple):
            return "<ext %s:%d>" % (os.path.basename(obj[1]), obj[2])
        t = obj.type.name
        if t == "GameObject":
            return obj.peek_name()
        if t in ("MonoBehaviour",) or t in COMPONENT_TYPES:
            d = obj.read()
            go = d.m_GameObject.deref() if hasattr(d, "m_GameObject") and d.m_GameObject.m_PathID else None
            if go is None:  # ScriptableObject (e.g. StatusEffect)
                return "%s(%s)" % (getattr(d, "m_Name", "?"), self.script_name(obj) if t == "MonoBehaviour" else t)
            gn = go.peek_name()
            return "%s(%s)" % (gn, self.script_name(obj) if t == "MonoBehaviour" else t)
        try:
            return obj.peek_name()
        except Exception:
            return "<%s %d>" % (t, obj.path_id)

    def pname(self, pptr, src):
        return self.name_of(self.deref(pptr, src))

    # ---- components -------------------------------------------------------------
    def script_name(self, mb):
        if mb.path_id in self._script_cache:
            return self._script_cache[mb.path_id]
        d = mb.read()
        n = None
        if d.m_Script.m_PathID != 0:
            s = d.m_Script.deref()
            n = s.read().m_ClassName if s else None
        self._script_cache[mb.path_id] = n
        return n

    def components(self, go):
        g = go.read()
        out = []
        for c in g.m_Component:
            co = c.component.deref()
            if co is None:
                continue
            t = co.type.name
            if t == "MonoBehaviour":
                t = self.script_name(co)
            out.append((t, co))
        return out

    def comp(self, go, typename):
        for t, co in self.components(go):
            if t == typename:
                return co
        return None

    def tt(self, obj):
        k = (id(obj.assets_file), obj.path_id)
        if k not in self._tt_cache:
            self._tt_cache[k] = obj.read_typetree(check_read=True)
        return self._tt_cache[k]

    def children(self, go):
        """Yield all descendant GameObjects (including go)."""
        g = go.read()
        tr = None
        for c in g.m_Component:
            co = c.component.deref()
            if co and co.type.name in ("Transform", "RectTransform"):
                tr = co
                break
        stack = [tr]
        while stack:
            t = stack.pop()
            td = t.read()
            yield td.m_GameObject.deref()
            for ch in td.m_Children:
                cc = ch.deref()
                if cc:
                    stack.append(cc)

    def active_in_hierarchy(self, go):
        """True if `go` and all of its ancestors have m_IsActive set (as saved in the prefab)."""
        while go is not None:
            g = go.read()
            if not g.m_IsActive:
                return False
            tr = None
            for c in g.m_Component:
                co = c.component.deref()
                if co and co.type.name in ("Transform", "RectTransform"):
                    tr = co
                    break
            if tr is None:
                return True
            f = tr.read().m_Father
            if f.m_PathID == 0:
                return True
            go = f.deref().read().m_GameObject.deref()
        return True

    def find_prefab(self, name, must_have=None):
        """Root GameObject named `name` (optionally having component `must_have`)."""
        res = []
        for o in self.go_by_name.get(name, []):
            g = o.read()
            # root = transform with no father
            for c in g.m_Component:
                co = c.component.deref()
                if co and co.type.name in ("Transform", "RectTransform"):
                    if co.read().m_Father.m_PathID == 0:
                        if must_have is None or self.comp(o, must_have):
                            res.append(o)
                    break
        return res


COMPONENT_TYPES = {"Transform", "Animator", "AudioSource", "ParticleSystem", "Light",
                   "SphereCollider", "CapsuleCollider", "BoxCollider", "Rigidbody", "MeshRenderer"}


def effectlist_names(A, el, src):
    """EffectList dict -> list of 'prefab' names (with flags)."""
    out = []
    for e in el.get("m_effectPrefabs", []):
        n = A.pname(e["m_prefab"], src)
        flags = []
        if not e.get("m_enabled", 1):
            flags.append("disabled")
        if e.get("m_attach"):
            flags.append("attach")
        if e.get("m_variant", -1) != -1:
            flags.append("variant=%d" % e["m_variant"])
        if e.get("m_childTransform"):
            flags.append("child=%s" % e["m_childTransform"])
        out.append(n + (" [%s]" % ",".join(flags) if flags else ""))
    return out

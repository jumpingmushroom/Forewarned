# dump_boss_attacks.py - dump Valheim boss prefabs, their attack items, the prefabs those attacks
# spawn (projectiles / AoEs / SpawnAbility / effects / ZSFX), and Animator attack timing.
#
# Usage (from repo root):
#   .venv/bin/python tools/dump_boss_attacks.py [out.json]
# Default output: gamedata/boss_dump.json (gamedata/ is gitignored). Also prints a short summary.
#
# Requirements: see tools/vhassets.py (UnityPy + TypeTreeGeneratorAPI, gamedata/Managed DLLs,
# SoftRef bundles c4210710 + 86c3d76e). Every MonoBehaviour is decoded with
# read_typetree(check_read=True), i.e. the decode must consume exactly the raw object length.
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vhassets import Assets, ROOT  # noqa: E402
import anim_timing  # noqa: E402

# Prefab names to dump. Bosses are also auto-discovered (any Humanoid with m_boss = true).
EXTRA_CHARS = ["Aspect_Yagluth", "Aspect_Elder"]

# Components whose fields we keep when dumping spawned prefabs
INTERESTING = {"Projectile", "Aoe", "SpawnAbility", "ZSFX", "TimedDestruction", "SpawnOnDamaged",
               "CharacterTimedDestruction", "TriggerSpawner", "SpawnArea", "LineConnect",
               "AnimationEffect", "CharacterAnimEvent", "EffectArea", "LevelEffects",
               "ProjectileTypeEffect", "Ragdoll", "RandomFlyingBird", "SE_Stats", "Destructible",
               "MineRock5", "SpawnPrefab", "Tameable", "Growup", "DelayedSpawn",
               "Turret", "ShieldGenerator", "TeleportAbility", "Teleport"}
SKIP_TYPES = {"ZNetView", "ZSyncTransform", "ZSyncAnimation", "LODGroup", "FootStep",
              "VisEquipment"}


class Dumper:
    def __init__(self, A):
        self.A = A
        self.prefabs = {}      # name -> dump of spawned prefab
        self.queue = []

    def clean(self, v, src, path=""):
        """Recursively convert a typetree dict: PPtrs -> names, EffectLists -> name lists."""
        A = self.A
        if isinstance(v, dict):
            if set(v.keys()) == {"m_FileID", "m_PathID"}:
                o = A.deref(v, src)
                if o is None:
                    return None
                if not isinstance(o, tuple) and o.type.name == "GameObject":
                    self.queue.append(o)
                elif not isinstance(o, tuple) and o.type.name == "MonoBehaviour":
                    d = o.read()
                    g = d.m_GameObject.deref() if d.m_GameObject.m_PathID else None
                    if g is not None:
                        self.queue.append(g)
                return A.name_of(o)
            if set(v.keys()) == {"m_effectPrefabs"}:
                out = []
                for e in v["m_effectPrefabs"]:
                    n = self.clean(e["m_prefab"], src)
                    extra = []
                    if not e.get("m_enabled", 1):
                        extra.append("DISABLED")
                    if e.get("m_attach"):
                        extra.append("attach")
                    if e.get("m_variant", -1) != -1:
                        extra.append("variant=%d" % e["m_variant"])
                    if e.get("m_childTransform"):
                        extra.append("child=" + e["m_childTransform"])
                    out.append("%s%s" % (n, (" [" + ",".join(extra) + "]") if extra else ""))
                return out
            return {k: self.clean(x, src, path + "." + k) for k, x in v.items()
                    if k not in ("m_GameObject", "m_Script", "m_Enabled", "m_ObjectHideFlags",
                                 "m_CorrespondingSourceObject", "m_PrefabInstance",
                                 "m_PrefabAsset", "m_EditorHideFlags", "m_EditorClassIdentifier",
                                 "m_Name")}
        if isinstance(v, list):
            return [self.clean(x, src, path) for x in v]
        if isinstance(v, float):
            return round(v, 4)
        return v

    def dump_go_components(self, go, deep=True):
        A = self.A
        out = []
        objs = list(A.children(go)) if deep else [go]
        for g in objs:
            for t, co in A.components(g):
                rec = None
                if t == "AudioSource":
                    d = co.read_typetree()
                    rec = {"maxDistance": round(d["MaxDistance"], 2), "minDistance": round(d["MinDistance"], 2),
                           "loop": d["Loop"], "rolloff": d["rolloffMode"], "volume": round(d["m_Volume"], 3),
                           "spatialBlend": None}
                elif t == "SphereCollider":
                    d = co.read()
                    rec = {"radius": round(d.m_Radius, 3), "isTrigger": d.m_IsTrigger}
                elif t == "CapsuleCollider":
                    d = co.read()
                    rec = {"radius": round(d.m_Radius, 3), "height": round(d.m_Height, 3), "isTrigger": d.m_IsTrigger}
                elif t == "BoxCollider":
                    d = co.read()
                    rec = {"size": [round(d.m_Size.x, 2), round(d.m_Size.y, 2), round(d.m_Size.z, 2)],
                           "center": [round(d.m_Center.x, 2), round(d.m_Center.y, 2), round(d.m_Center.z, 2)], "isTrigger": d.m_IsTrigger}
                elif t == "Animator":
                    d = co.read()
                    rec = {"controller": A.name_of(d.m_Controller.deref()) if d.m_Controller.m_PathID else None}
                elif t in INTERESTING or (t not in SKIP_TYPES and t not in (
                        "Transform", "MeshFilter", "MeshRenderer", "ParticleSystem",
                        "ParticleSystemRenderer", "Light", "SkinnedMeshRenderer", "Rigidbody",
                        "MeshCollider", "LineRenderer", "TrailRenderer", "Cloth",
                        "CharacterJoint", "ParticleSystemForceField", "LODGroup", "RectTransform")
                        and co.type.name == "MonoBehaviour"):
                    try:
                        rec = self.clean(A.tt(co), co)
                    except Exception as e:  # noqa
                        rec = {"DECODE_ERROR": str(e)}
                if rec is not None:
                    out.append({"go": g.peek_name(), "type": t, "data": rec,
                                "active": A.active_in_hierarchy(g)})
        return out

    def dump_item(self, go):
        A = self.A
        idrop = A.comp(go, "ItemDrop")
        if idrop is None:
            return {"prefab": go.peek_name(), "error": "no ItemDrop"}
        d = A.tt(idrop)
        sh = d["m_itemData"]["m_shared"]
        sh = self.clean(sh, idrop)
        # drop bulky irrelevant fields
        for k in list(sh.keys()):
            if k in ("m_icons", "m_description", "m_armorMaterial", "m_setStatusEffect",
                     "m_equipStatusEffect", "m_appendToolTip", "m_trophyPos"):
                sh.pop(k, None)
        return {"prefab": go.peek_name(), "shared": sh}

    def run(self):
        A = self.A
        bosses = {}
        for sf in A.cabs.values():
            for pid, o in sf.objects.items():
                if o.type.name != "MonoBehaviour":
                    continue
                if A.script_name(o) != "Humanoid":
                    continue
                d = A.tt(o)
                g = A.deref(d["m_GameObject"], o)
                name = g.peek_name()
                if d["m_boss"] or name in EXTRA_CHARS or name.startswith("Aspect_"):
                    bosses[name] = (g, o, d)
        result = {"bosses": {}, "prefabs": {}}
        for name, (g, hum, d) in sorted(bosses.items()):
            rec = {}
            ch = {k: d[k] for k in ("m_name", "m_group", "m_faction", "m_boss", "m_bossOrder",
                                    "m_dontHideBossHud", "m_bossEvent", "m_defeatSetGlobalKey",
                                    "m_health", "m_flying", "m_speed", "m_runSpeed", "m_turnSpeed")}
            rec["character"] = ch
            items = {}
            for key in ("m_defaultItems", "m_randomWeapon", "m_randomArmor", "m_randomShield"):
                items[key] = [A.pname(p, hum) for p in d[key]]
            items["m_randomSets"] = [{"name": s["m_name"], "items": [A.pname(p, hum) for p in s["m_items"]]}
                                     for s in d["m_randomSets"]]
            items["m_randomItems"] = [{"prefab": A.pname(s["m_prefab"], hum), "chance": s["m_chance"]}
                                      for s in d["m_randomItems"]]
            rec["items"] = items
            rec["character_effects"] = {k: self.clean(d[k], hum) for k in (
                "m_hitEffects", "m_deathEffects", "m_equipEffects")}
            mai = A.comp(g, "MonsterAI")
            if mai:
                md = A.tt(mai)
                keep = ("m_viewRange", "m_viewAngle", "m_hearRange", "m_alertRange", "m_minAttackInterval",
                        "m_circulateWhileCharging", "m_circulateWhileChargingFlying", "m_maxChaseDistance",
                        "m_randomFly", "m_chanceToTakeoff", "m_chanceToLand", "m_groundDuration",
                        "m_airDuration", "m_takeoffTime", "m_flyAltitudeMin", "m_flyAltitudeMax",
                        "m_interceptTimeMin", "m_interceptTimeMax", "m_circleTargetInterval",
                        "m_circleTargetDuration", "m_circleTargetDistance", "m_spawnMessage",
                        "m_deathMessage", "m_alertedMessage", "m_noiseWakeup", "m_hearRange", "m_sleeping", "m_wakeupRange",
                        "m_alertedEffects", "m_idleSound", "m_wakeupEffects", "m_randomMoveInterval",
                        "m_randomMoveRange", "m_idleSoundInterval", "m_idleSoundChance")
                rec["monsterAI"] = {k: self.clean(md[k], mai) for k in keep if k in md}
            # all other components on the boss hierarchy (non-standard ones)
            comps = []
            for cg in A.children(g):
                for t, co in A.components(cg):
                    if co.type.name == "MonoBehaviour" and t not in ("Humanoid", "MonsterAI") \
                            and t not in SKIP_TYPES:
                        try:
                            cd = self.clean(A.tt(co), co)
                        except Exception as e:  # noqa
                            cd = {"DECODE_ERROR": str(e)}
                        comps.append({"go": cg.peek_name(), "type": t, "data": cd})
                    elif t == "Animator":
                        ad = co.read()
                        comps.append({"go": cg.peek_name(), "type": "Animator",
                                      "controller": A.name_of(ad.m_Controller.deref()) if ad.m_Controller.m_PathID else None})
                        rec.setdefault("animators", []).append(co)
            rec["components"] = comps
            # items
            rec["attack_items"] = []
            allitems = {}
            for key in ("m_defaultItems", "m_randomWeapon"):
                for p in d[key]:
                    _o = A.deref(p, hum); allitems[_o.path_id if _o is not None and not isinstance(_o, tuple) else None] = _o
            for s in d["m_randomSets"]:
                for p in s["m_items"]:
                    _o = A.deref(p, hum); allitems[_o.path_id if _o is not None and not isinstance(_o, tuple) else None] = _o
            for s in d["m_randomItems"]:
                _o = A.deref(s["m_prefab"], hum); allitems[_o.path_id if _o is not None and not isinstance(_o, tuple) else None] = _o
            for it in sorted([x for x in allitems.values() if x is not None and not isinstance(x, tuple)],
                             key=lambda x: x.peek_name()):
                rec["attack_items"].append(self.dump_item(it))
            # animation timing
            anims = rec.pop("animators", [])
            rec["animation"] = []
            for an in anims:
                try:
                    rec["animation"].append(anim_timing.dump_animator(A, an))
                except Exception as e:  # noqa
                    import traceback
                    rec["animation"].append({"error": traceback.format_exc()})
            result["bosses"][name] = rec
        # spawned prefabs (BFS)
        self.queue.extend(anim_timing.ANIM_REFS)
        seen = set(n for n in bosses)
        while self.queue:
            go = self.queue.pop(0)
            n = go.peek_name()
            if n in seen or n in result["prefabs"]:
                continue
            seen.add(n)
            # don't recurse into other characters; just mark
            if A.comp(go, "Humanoid") or A.comp(go, "Character"):
                hc = A.comp(go, "Humanoid") or A.comp(go, "Character")
                hd = A.tt(hc)
                result["prefabs"][n] = {"character": True, "m_name": hd["m_name"], "m_health": hd["m_health"]}
                continue
            if A.comp(go, "ItemDrop"):
                result["prefabs"][n] = {"item": self.dump_item(go)}
                continue
            result["prefabs"][n] = {"components": self.dump_go_components(go)}
        return result


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "gamedata/boss_dump.json")
    A = Assets()
    res = Dumper(A).run()
    with open(out, "w") as f:
        json.dump(res, f, indent=1, default=str)
    for n, b in res["bosses"].items():
        print(n, b["character"]["m_name"], b["character"]["m_health"],
              [i["prefab"] for i in b["attack_items"]])
    print("prefabs dumped:", len(res["prefabs"]), "->", out)


if __name__ == "__main__":
    main()

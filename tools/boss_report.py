# boss_report.py - turn gamedata/boss_dump.json (from dump_boss_attacks.py) + gamedata/loc_en.json
# (from loc_en.py) into markdown tables: per boss attacks, AI gating, geometry, wind-up timing,
# warning sounds, spawned-prefab chains, and a sound cross-reference with AudioSource maxDistance.
#
# Usage (from repo root):
#   .venv/bin/python tools/dump_boss_attacks.py && .venv/bin/python tools/loc_en.py
#   .venv/bin/python tools/boss_report.py > some.md
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
D = json.load(open(os.path.join(ROOT, "gamedata/boss_dump.json")))
LOC = json.load(open(os.path.join(ROOT, "gamedata/loc_en.json")))
P = D["prefabs"]

ATYPE = ["Horizontal", "Vertical", "Projectile", "None", "Area", "TriggerProjectile"]
AITARGET = ["Enemy", "FriendHurt", "Friend"]
STARGET = ["ClosestEnemy", "RandomEnemy", "Caster", "Position", "RandomPathfindablePosition"]
PTFLAGS = ["Arrow", "Magic", "Bomb", "Physical", "Bolt", "Missile", "Spear", "Tar", "Fire", "Lava", "Posion",
           "Smoke", "Frost", "Catapult", "AOE", "Harpoon", "Nature", "Lightning", "Summon"]


def ptype(v):
    return "|".join(n for i, n in enumerate(PTFLAGS) if v & (1 << i)) or "None"


def scalars(d, limit=14):
    out = []
    for k, v in d.items():
        if isinstance(v, (int, float, str)) and v not in (0, 0.0, "") and not k.startswith("m_Editor"):
            out.append("%s=%s" % (k[2:] if k.startswith("m_") else k, v))
        elif isinstance(v, list) and v and all(isinstance(x, str) for x in v):
            out.append("%s=[%s]" % (k[2:] if k.startswith("m_") else k, ", ".join(v)))
    return ", ".join(out[:limit])

ORDER = ["Fader", "Dragon", "GoblinKing", "gd_king", "Bonemass", "SeekerQueen", "Eikthyr",
         "FrozenKing", "FrozenKing_p2", "FrozenKing_p3",
         "Aspect_Eikthyr", "Aspect_Elder", "Aspect_Bonemass", "Aspect_Moder", "Aspect_Yagluth",
         "Aspect_SeekerQueen", "Aspect_Fader", "Aspect_TentaRoot", "Hive", "TheHive"]


def loc(tok):
    if not tok:
        return ""
    if tok.startswith("$"):
        v = LOC.get(tok[1:])
        return "%s (\"%s\")" % (tok, v) if v else "%s (no English string)" % tok
    return "\"%s\" (literal)" % tok


def base(n):
    return n.split(" [")[0] if n else n


def fx(lst):
    return ", ".join(lst) if lst else ""


def dmg(d):
    parts = ["%s %g" % (k[2:], v) for k, v in d.items() if v and k not in ("m_chop", "m_pickaxe")]
    return ", ".join(parts) or "-"


def triggers(a):
    an = a["m_attackAnimation"]
    if not an:
        return []
    if a["m_attackChainLevels"] > 1:
        return [an + str(i) for i in range(a["m_attackChainLevels"])]
    if a["m_attackRandomAnimations"] >= 2:
        return [an + str(i) for i in range(a["m_attackRandomAnimations"])]
    return [an]


def anim_entries(boss, trig):
    out = []
    for an in boss["animation"]:
        if "triggers" not in an or not an.get("active", True):
            continue
        for e in an["triggers"].get(trig, []):
            out.append(e)
    anyfirst = [e for e in out if e["from"] == "Any"]
    return anyfirst or out


def pre_trigger_fx(e):
    """Effect/Attach events up to (and including) the first OnAttackTrigger/Hit."""
    res = []
    for x in e["timeline"]:
        if x["fn"] in ("Hit", "OnAttackTrigger"):
            break
        if x["fn"] in ("Effect", "Attach", "TrailOn") and x["param"] != "jump":
            res.append("%s@%.2fs" % (x["param"] if x["fn"] != "TrailOn" else "TrailOn", x["t_real"]))
    return res


def all_hits(e):
    return [x["t_real"] for x in e["timeline"] if x["fn"] in ("Hit", "OnAttackTrigger")]


def geometry(a):
    t = ATYPE[a["m_attackType"]]
    if t in ("Horizontal", "Vertical"):
        return "melee sweep: range %g m, angle %g deg, ray width %g, height %g%s" % (
            a["m_attackRange"], a["m_attackAngle"], a["m_attackRayWidth"], a["m_attackHeight"],
            (", maxYAngle %g" % a["m_maxYAngle"]) if a["m_maxYAngle"] else "")
    if t == "Area":
        return "sphere r=%g m at %g m forward, %g m up" % (a["m_attackRayWidth"], a["m_attackRange"], a["m_attackHeight"])
    if t == "Projectile":
        s = "projectile %s x%d" % (a["m_attackProjectile"], a["m_projectiles"])
        if a["m_projectileBursts"] > 1:
            s += " x %d bursts every %gs" % (a["m_projectileBursts"], a["m_burstInterval"])
        s += ", vel %g (min %g), spread %g" % (a["m_projectileVel"], a["m_projectileVelMin"], a["m_projectileAccuracy"])
        return s
    return "no hit (None)" + ((", spawns " + a["m_spawnOnTrigger"]) if a.get("m_spawnOnTrigger") else "")


def comp(pname, typ):
    p = P.get(base(pname) or "")
    if not p or "components" not in p:
        return []
    return [c for c in p["components"] if c["type"] == typ]


def colliders(pname):
    out = []
    for t in ("SphereCollider", "BoxCollider", "CapsuleCollider"):
        for c in comp(pname, t):
            if c["data"].get("isTrigger"):
                d = c["data"]
                if t == "SphereCollider":
                    out.append("trigger sphere r=%g" % d["radius"])
                elif t == "BoxCollider":
                    out.append("trigger box %s" % "x".join("%g" % v for v in d["size"]))
                else:
                    out.append("trigger capsule r=%g h=%g" % (d["radius"], d["height"]))
    return out


def chain(pname, depth=0, seen=None):
    """Describe the prefab chain an attack spawns (Projectile / Aoe / SpawnAbility)."""
    seen = seen or set()
    pname = base(pname)
    if not pname or pname in seen or depth > 6:
        return []
    seen.add(pname)
    p = P.get(pname)
    ind = "  " * depth
    if p is None:
        return ["%s- %s (not dumped)" % (ind, pname)]
    if p.get("character"):
        return ["%s- %s: creature %s, hp %g" % (ind, pname, loc(p["m_name"]), p["m_health"])]
    if "item" in p:
        return ["%s- %s: item" % (ind, pname)]
    lines = []
    for c in p["components"]:
        d = c["data"]
        if c["type"] == "Projectile":
            lines.append("%s- %s [Projectile] type %s, ttl %g, gravity %g, aoe r=%g, dmg %s%s%s" % (
                ind, pname, ptype(d["m_type"]), d["m_ttl"], d["m_gravity"], d["m_aoe"], dmg(d["m_damage"]),
                (", spawnOnHit %s (x%d)" % (d["m_spawnOnHit"], d["m_spawnCount"])) if d.get("m_spawnOnHit") else "",
                (", hitEffects %s" % fx(d["m_hitEffects"])) if d.get("m_hitEffects") else ""))
            lines += chain(d.get("m_spawnOnHit"), depth + 1, seen)
        elif c["type"] == "Aoe":
            shape = "box collider" if d.get("m_useCollider") else "sphere r=%g" % d["m_radius"]
            trig = colliders(pname) if d.get("m_useTriggers") else []
            lines.append("%s- %s [Aoe%s] %s%s, useAttackSettings %d, activationDelay %g, ttl %g%s, hitInterval %g, dmg %s%s%s" % (
                ind, pname, "@" + c["go"] if c["go"] != pname else "", shape,
                (" (useTriggers: %s)" % ", ".join(trig)) if d.get("m_useTriggers") else "",
                d["m_useAttackSettings"], d["m_activationDelay"], d["m_ttl"],
                ("-%g" % d["m_ttlMax"]) if d.get("m_ttlMax") else "", d["m_hitInterval"], dmg(d["m_damage"]),
                (", spawnOnHitTerrain %s" % d["m_spawnOnHitTerrain"]) if d.get("m_spawnOnHitTerrain") else "",
                (", hitEffects %s" % fx(d["m_hitEffects"])) if d.get("m_hitEffects") else ""))
            if d.get("m_spawnOnHitTerrain"):
                lines += chain(d["m_spawnOnHitTerrain"], depth + 1, seen)
        elif c["type"] == "SpawnAbility":
            lines.append("%s- %s [SpawnAbility] spawns %s x%d-%d (maxSpawned %d), radius %g, circle %d, atTarget %d, groundOffset %g, initialDelay %g, delay %g each, targetType %s, maxTargetRange %g%s%s" % (
                ind, pname, fx(d["m_spawnPrefab"]), d["m_minToSpawn"], d["m_maxToSpawn"], d["m_maxSpawned"],
                d["m_spawnRadius"], d["m_circleSpawn"], d["m_spawnAtTarget"], d["m_spawnGroundOffset"],
                d["m_initialSpawnDelay"], d["m_spawnDelay"], STARGET[d["m_targetType"]], d["m_maxTargetRange"],
                (", projVel %g acc %g" % (d["m_projectileVelocity"], d["m_projectileAccuracy"])) if d.get("m_projectileVelocity") else "",
                (", spawnEffects %s" % fx(d["m_spawnEffects"])) if d.get("m_spawnEffects") else ""))
            for sp in d["m_spawnPrefab"]:
                lines += chain(sp, depth + 1, seen)
        elif c["type"] in ("TriggerSpawnAbility", "TeleportAbility", "Destructible") and isinstance(d, dict):
            lines.append("%s- %s [%s] %s" % (ind, pname, c["type"], scalars(d)))
            for k, v in d.items():
                if isinstance(v, str) and v in P and v != pname:
                    lines += chain(v, depth + 1, seen)
                if isinstance(v, list):
                    for x in v:
                        if isinstance(x, str) and base(x) in P:
                            lines += chain(x, depth + 1, seen)
    if not lines:
        lines.append("%s- %s (no Projectile/Aoe/SpawnAbility component)" % (ind, pname))
    return lines


def sound_info(name):
    n = base(name)
    p = P.get(n)
    if not p or "components" not in p:
        return None
    z = [c for c in p["components"] if c["type"] == "ZSFX"]
    a = [c for c in p["components"] if c["type"] == "AudioSource"]
    if not z:
        return None
    md = max((c["data"]["maxDistance"] for c in a), default=None)
    cap = z[0]["data"].get("m_closedCaptionToken", "")
    return {"maxDistance": md, "caption": cap, "loop": any(c["data"]["loop"] for c in a)}


def area_summary(a):
    """Short 'what area does it hit' text for the summary table."""
    t = ATYPE[a["m_attackType"]]
    if t in ("Horizontal", "Vertical"):
        return "cone %g m / %g deg" % (a["m_attackRange"], a["m_attackAngle"])
    if t == "Area":
        return "sphere r %g m (%g m ahead)" % (a["m_attackRayWidth"], a["m_attackRange"])
    if t == "None":
        return "none (no hit)"
    parts = []
    for line in chain(a["m_attackProjectile"]):
        l = line.strip()
        if "[Aoe" in l:
            nm = l[2:].split(" ")[0]
            shape = l.split("] ", 1)[1].split(",")[0]
            parts.append("%s: %s" % (nm, shape))
        elif "[Projectile]" in l and "aoe r=" in l:
            r = l.split("aoe r=")[1].split(",")[0]
            if r != "0":
                parts.append("%s: aoe r %s" % (l[2:].split(" ")[0], r))
        elif "[SpawnAbility]" in l:
            nm = l[2:].split(" ")[0]
            sp = l.split("spawns ")[1].split(" (")[0]
            rad = l.split("radius ")[1].split(",")[0]
            parts.append("%s spawns %s within r %s" % (nm, sp, rad))
    return "; ".join(parts) or "projectile"


def summary(bname, b):
    rows = []
    for it in b["attack_items"]:
        sh = it["shared"]
        a = sh["m_attack"]
        for tg in triggers(a) or ["-"]:
            ents = anim_entries(b, tg)
            if not ents:
                rows.append((it["prefab"], tg, "no animator transition", "", "", area_summary(a)))
                continue
            e = ents[0]
            pre = [f.replace("@", " @") for f in pre_trigger_fx(e) if not f.startswith("TrailOn")]
            start = [x for x in (sh.get("m_startEffect") or []) + (a.get("m_startEffect") or []) if "DISABLED" not in x]
            trig = [x for x in (sh.get("m_triggerEffect") or []) + (a.get("m_triggerEffect") or []) if "DISABLED" not in x]
            rows.append((it["prefab"], tg, "%s" % e["windup_s"], ", ".join(pre) or "-",
                         "start: %s; trigger: %s" % (", ".join(start) or "-", ", ".join(trig) or "-"), area_summary(a)))
    return rows


def main():
    out = []
    w = out.append
    w("## Wind-up summary (main bosses)\n")
    w("Wind-up = seconds from the animator trigger (Attack.Start -> SetTrigger) to the first OnAttackTrigger/Hit "
      "animation event, including Speed() events and state speed. shared/attack startEffect fires when the "
      "animator enters the attack-tagged state (about 0 s + transition); triggerEffect fires at the hit.\n")
    for bname in ["Fader", "Dragon", "GoblinKing", "gd_king", "Bonemass", "SeekerQueen", "Eikthyr", "FrozenKing", "FrozenKing_p3"]:
        b = D["bosses"][bname]
        w("\n**%s** (%s)\n" % (bname, LOC.get(b["character"]["m_name"][1:], "")))
        w("| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |")
        w("|---|---|---|---|---|---|")
        for r in summary(bname, b):
            w("| %s |" % " | ".join(r))
    w("\n## Per-boss detail\n")
    sounds = {}  # sound -> list of usages

    def use(snd, where):
        if snd and "DISABLED" not in snd:
            sounds.setdefault(base(snd), []).append(where)

    for bname in ORDER + [b for b in D["bosses"] if b not in ORDER]:
        b = D["bosses"].get(bname)
        if b is None:
            continue
        ch = b["character"]
        ai = b.get("monsterAI", {})
        w("\n### %s\n" % bname)
        w("- name: %s; health %g; m_boss %d; bossEvent `%s`; defeat key `%s`; flying %d" % (
            loc(ch["m_name"]), ch["m_health"], ch["m_boss"], ch["m_bossEvent"], ch["m_defeatSetGlobalKey"], ch["m_flying"]))
        w("- MonsterAI: minAttackInterval %s s; viewRange %s; alertRange %s; sleeping %s (wakeupRange %s); spawnMessage %s; alertedMessage %s; deathMessage %s; alertedEffects %s; idleSound %s" % (
            ai.get("m_minAttackInterval"), ai.get("m_viewRange"), ai.get("m_alertRange"), ai.get("m_sleeping"),
            ai.get("m_wakeupRange"), loc(ai.get("m_spawnMessage")) or "-", loc(ai.get("m_alertedMessage")) or "-",
            loc(ai.get("m_deathMessage")) or "-", fx(ai.get("m_alertedEffects")) or "-", fx(ai.get("m_idleSound")) or "-"))
        anims = ["%s:%s%s" % (a.get("animator_go"), a.get("controller"), "" if a.get("active", True) else " (inactive)")
                 for a in b["animation"] if "controller" in a]
        w("- Animator(s): %s" % ", ".join(anims))
        items = b["items"]
        w("- m_defaultItems: %s%s%s" % (fx(items["m_defaultItems"]),
                                         ("; m_randomWeapon: " + fx(items["m_randomWeapon"])) if items["m_randomWeapon"] else "",
                                         ("; m_randomSets: " + json.dumps(items["m_randomSets"])) if items["m_randomSets"] else ""))
        w("")
        w("| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |")
        w("|---|---|---|---|---|---|---|---|---|---|---|---|")
        details = []
        for it in b["attack_items"]:
            sh = it["shared"]
            a = sh["m_attack"]
            sa = sh["m_secondaryAttack"]
            trigs = triggers(a)
            flags = []
            if sh["m_aiPrioritized"]:
                flags.append("prioritized")
            if sh.get("m_aiPrioritizedIfAngleCheckValid"):
                flags.append("prioIfAngleOK")
            if sh["m_aiWhenFlying"]:
                flags.append("flying(alt %g-%g)" % (sh["m_aiWhenFlyingAltitudeMin"], sh["m_aiWhenFlyingAltitudeMax"]))
            if sh["m_aiWhenWalking"]:
                flags.append("walking")
            if sh["m_aiWhenSwiming"]:
                flags.append("swimming")
            if sh["m_aiInDungeonOnly"]:
                flags.append("dungeonOnly")
            if sh.get("m_aiInMistOnly"):
                flags.append("mistOnly")
            if sh.get("m_aiInvertAngleCheck"):
                flags.append("invertAngle")
            if sh["m_aiTargetType"]:
                flags.append("target=" + AITARGET[sh["m_aiTargetType"]])
            gate = ""
            if sh["m_aiMaxHealthPercentage"] < 1 or sh["m_aiMinHealthPercentage"] > 0:
                gate = "%g-%g%%" % (sh["m_aiMinHealthPercentage"] * 100, sh["m_aiMaxHealthPercentage"] * 100)
            wind = []
            for tg in trigs:
                for e in anim_entries(b, tg):
                    hits = all_hits(e)
                    wind.append("%s: **%s** (%s; %s)" % (tg, e["windup_s"], e["state"].replace("Base Layer.", ""),
                                                          ", ".join("%g" % h for h in hits) or "no hit event"))
                    for f in pre_trigger_fx(e):
                        nm, t = f.rsplit("@", 1)
                        use(nm, "%s %s: anim event %s at %s (pre-hit)" % (bname, it["prefab"], tg, t))
                    for x in e["timeline"]:
                        if x["fn"] in ("Effect", "Attach") and x["param"] not in (None, "jump"):
                            pass
            w("| %s | %s | %s | %s | %g (%g) | %g | %g | %s | %s | %s | %s | %s |" % (
                it["prefab"], loc(sh["m_name"]), ", ".join(trigs) or "-", ATYPE[a["m_attackType"]],
                sh["m_aiAttackRange"], sh["m_aiAttackRangeMin"], sh["m_aiAttackInterval"], sh["m_aiAttackMaxAngle"],
                gate or "-", ", ".join(flags), geometry(a), dmg(sh["m_damages"]), "; ".join(wind) or "n/a"))
            # effects
            efx = []
            for k, lab in (("m_startEffect", "start"), ("m_triggerEffect", "trigger"), ("m_trailStartEffect", "trailStart"),
                           ("m_hitEffect", "hit")):
                if sh.get(k):
                    efx.append("shared.%s: %s" % (lab, fx(sh[k])))
                    for s in sh[k]:
                        use(s, "%s %s: shared %sEffect" % (bname, it["prefab"], lab))
                if a.get(k):
                    efx.append("attack.%s: %s" % (lab, fx(a[k])))
                    for s in a[k]:
                        use(s, "%s %s: attack %sEffect" % (bname, it["prefab"], lab))
            if a.get("m_burstEffect"):
                efx.append("attack.burst: %s" % fx(a["m_burstEffect"]))
                for s in a["m_burstEffect"]:
                    use(s, "%s %s: attack burstEffect" % (bname, it["prefab"]))
            ch_lines = []
            if a.get("m_attackProjectile"):
                ch_lines += chain(a["m_attackProjectile"])
            if a.get("m_spawnOnTrigger"):
                ch_lines += chain(a["m_spawnOnTrigger"])
            extra = []
            for k in ("m_attackOriginJoint", "m_loopingAttack", "m_toggleFlying", "m_attach", "m_chargeAnimationBool",
                      "m_useCharacterFacing", "m_launchAngle", "m_hitThroughWalls", "m_speedFactor", "m_speedFactorRotation"):
                if a.get(k):
                    extra.append("%s=%s" % (k[2:], a[k]))
            if sa.get("m_attackAnimation"):
                extra.append("SECONDARY attack anim %s type %s (not used by MonsterAI)" % (sa["m_attackAnimation"], ATYPE[sa["m_attackType"]]))
            details.append((it["prefab"], efx, ch_lines, extra))
        w("")
        for name, efx, ch_lines, extra in details:
            w("- **%s**: %s%s" % (name, "; ".join(efx) or "no effects", ("; " + ", ".join(extra)) if extra else ""))
            for l in ch_lines:
                w("  " + l)
        # anim triggers not used by any attack (taunts, jumps, etc.)
        used = set()
        for it in b["attack_items"]:
            used.update(triggers(it["shared"]["m_attack"]))
        others = []
        for an in b["animation"]:
            if not an.get("active", True) or "triggers" not in an:
                continue
            for tg, ents in an["triggers"].items():
                if tg in used or tg in ("stagger", "attack_abort", "detach", "attach", "emote_stop"):
                    continue
                for e in ents[:1]:
                    pf = pre_trigger_fx(e)
                    others.append("%s -> %s (hit at %s; fx %s)" % (tg, e["state"].replace("Base Layer.", ""),
                                                                  e["windup_s"], ", ".join(pf) or "-"))
                    for f in pf:
                        nm, t = f.rsplit("@", 1)
                        use(nm, "%s: anim trigger %s (not an attack item) at %s" % (bname, tg, t))
        if others:
            w("- other animator triggers (not from attack items): " + "; ".join(sorted(set(others))))
    # sounds
    w("\n## Sound cross-reference (ZSFX prefabs only)\n")
    w("| sound prefab | maxDistance | caption token | loop | where it plays |")
    w("|---|---|---|---|---|")
    for s in sorted(sounds):
        si = sound_info(s)
        if not si:
            continue
        w("| %s | %s | %s | %s | %s |" % (s, si["maxDistance"], si["caption"] or "-", "yes" if si["loop"] else "",
                                          "<br>".join(sorted(set(sounds[s])))))
    print("\n".join(out))


if __name__ == "__main__":
    main()

# anim_timing.py - parse a (runtime-compiled) AnimatorController / AnimatorOverrideController from
# a bundle and compute, per trigger parameter, the state(s) it enters, the clips played, the clip
# AnimationEvents and the wind-up time (trigger -> first OnAttackTrigger/Hit event).
#
# Used by dump_boss_attacks.py; not meant to be run directly.
#
# Timing model:
#   real time = clip time / (state.m_Speed * animator.speed). animator.speed starts at 1 (it is reset
#   to 1 by CharacterAnimEvent.CustomFixedUpdate whenever the character is not in an attack) and is
#   changed by "Speed(float)" animation events (CharacterAnimEvent.Speed) from that event onwards.
#   Transition blend time is ignored (the destination state starts playing at transition start, at
#   the transition's m_TransitionOffset (normalized time) into its clip).
#   If the entered state's clip has no OnAttackTrigger/Hit event, unconditional exit-time transitions
#   are followed (adds exitTime * clipLength) to find the event in a follow-up state.
import binascii

TRIGGER_FUNCS = ("OnAttackTrigger", "Hit")
ANIM_REFS = []  # GameObjects referenced by AnimationEvents (Effect/Attach prefabs); consumed by the dumper
COND = {1: "If", 2: "IfNot", 3: "Greater", 4: "Less", 6: "Equals", 7: "NotEqual"}
PTYPE = {1: "Float", 3: "Int", 4: "Bool", 9: "Trigger"}


def crc(s):
    return binascii.crc32(s.encode()) & 0xFFFFFFFF


def _d(x):
    return x["data"] if isinstance(x, dict) and "data" in x and len(x) == 1 else x


def clip_info(A, clip_obj):
    c = clip_obj.read()
    length = c.m_MuscleClip.m_StopTime - c.m_MuscleClip.m_StartTime
    evs = []
    for e in (c.m_Events or []):
        ref = None
        if e.objectReferenceParameter.m_PathID != 0:
            ro = A.deref(e.objectReferenceParameter, clip_obj)
            ref = A.name_of(ro)
            if ro is not None and not isinstance(ro, tuple) and ro.type.name == "GameObject":
                ANIM_REFS.append(ro)
        evs.append({"t": round(e.time, 4), "fn": e.functionName, "str": e.data,
                    "float": round(e.floatParameter, 4), "int": e.intParameter, "obj": ref})
    return {"clip": c.m_Name, "length": round(length, 4), "events": evs}


def dump_animator(A, animator):
    ad = animator.read()
    ctrl = A.deref(ad.m_Controller, animator)
    override = {}
    out = {"animator_go": ad.m_GameObject.deref().peek_name(), "controller": ctrl.peek_name(),
           "active": A.active_in_hierarchy(ad.m_GameObject.deref()), "enabled": bool(ad.m_Enabled)}
    if ctrl.type.name == "AnimatorOverrideController":
        od = ctrl.read_typetree()
        base = A.deref(od["m_Controller"], ctrl)
        for p in od["m_Clips"]:
            a = A.deref(p["m_OriginalClip"], ctrl)
            b = A.deref(p["m_OverrideClip"], ctrl)
            if a is not None and b is not None:
                override[a.path_id] = b
        out["override_of"] = base.peek_name()
        ctrl = base
    tt = ctrl.read_typetree()
    tos = {h & 0xFFFFFFFF: s for h, s in tt["m_TOS"]}
    clips = []
    for p in tt["m_AnimationClips"]:
        co = A.deref(p, ctrl)
        if co is not None and co.path_id in override:
            co = override[co.path_id]
        clips.append(co)
    clipcache = {}

    def cinfo(i):
        if i not in clipcache:
            co = clips[i]
            clipcache[i] = clip_info(A, co) if co is not None and not isinstance(co, tuple) else {"clip": None, "length": 0, "events": []}
        return clipcache[i]

    C = _d(tt["m_Controller"])
    params = {}
    for v in _d(C["m_Values"])["m_ValueArray"]:
        params[v["m_ID"] & 0xFFFFFFFF] = (tos.get(v["m_ID"] & 0xFFFFFFFF, "#%x" % v["m_ID"]), PTYPE.get(v["m_Type"], v["m_Type"]))
    layers = []
    for li, layer in enumerate(C["m_LayerArray"]):
        L = _d(layer)
        sm = _d(C["m_StateMachineArray"][L["m_StateMachineIndex"]])
        states = []
        for s in sm["m_StateConstantArray"]:
            s = _d(s)
            name = tos.get(s["m_FullPathID"] & 0xFFFFFFFF) or tos.get(s["m_NameID"] & 0xFFFFFFFF) or "#%x" % s["m_FullPathID"]
            cl = []
            for bt in s["m_BlendTreeConstantArray"]:
                for n in _d(bt)["m_NodeArray"]:
                    n = _d(n)
                    if n["m_ClipID"] != 0xFFFFFFFF:
                        cl.append(n["m_ClipID"])
            trans = [_tr(_d(t), tos, params) for t in s["m_TransitionConstantArray"]]
            states.append({"name": name, "speed": s["m_Speed"],
                           "speedParam": params.get(s["m_SpeedParamID"] & 0xFFFFFFFF, (None,))[0] if s["m_SpeedParamID"] else None,
                           "tag": tos.get(s["m_TagID"] & 0xFFFFFFFF, s["m_TagID"]), "clips": cl, "trans": trans})
        anyt = [_tr(_d(t), tos, params) for t in sm["m_AnyStateTransitionConstantArray"]]
        layers.append({"index": li, "states": states, "any": anyt})
    out["params"] = {n: t for n, t in params.values()}
    # triggers -> entries
    res = {}
    for li, layer in enumerate(layers):
        states = layer["states"]
        cands = []
        for t in layer["any"]:
            cands.append(("Any", t))
        for s in states:
            for t in s["trans"]:
                cands.append((s["name"], t))
        for src, t in cands:
            for cnd in t["conds"]:
                if cnd[0] == "If" and out["params"].get(cnd[1]) == "Trigger":
                    trig = cnd[1]
                    dst = states[t["dest"]] if 0 <= t["dest"] < len(states) else None
                    if dst is None:
                        continue
                    entry = {"layer": li, "from": src, "conds": t["conds"], "state": dst["name"],
                             "stateSpeed": round(dst["speed"], 4), "speedParam": dst["speedParam"], "tag": dst["tag"]}
                    entry["transOffset"] = t["offset"]
                    entry["transDuration"] = t["dur"]
                    entry.update(_windup(states, t["dest"], cinfo, offset=t["offset"]))
                    res.setdefault(trig, []).append(entry)
    out["triggers"] = res
    return out


def _tr(t, tos, params):
    conds = []
    for c in t["m_ConditionConstantArray"]:
        c = _d(c)
        pn = params.get(c["m_EventID"] & 0xFFFFFFFF, (tos.get(c["m_EventID"] & 0xFFFFFFFF, "#%x" % c["m_EventID"]),))[0]
        conds.append((COND.get(c["m_ConditionMode"], c["m_ConditionMode"]), pn, round(c["m_EventThreshold"], 3)))
    return {"conds": conds, "dest": t["m_DestinationState"], "hasExit": t["m_HasExitTime"],
            "exit": round(t["m_ExitTime"], 4), "dur": round(t["m_TransitionDuration"], 4),
            "offset": round(t["m_TransitionOffset"], 4),
            "fixed": t["m_HasFixedDuration"]}


def _windup(states, idx, cinfo, depth=0, offset=0.0):
    """Follow the state chain from `idx`; return clips, a real-time event timeline and the time to
    the first attack-trigger event (OnAttackTrigger/Hit)."""
    chain = []
    timeline = []
    t_real = 0.0
    anim_speed = 1.0
    visited = set()
    found = None
    while idx not in visited and depth < 6:
        visited.add(idx)
        depth += 1
        st = states[idx]
        if not st["clips"]:
            chain.append({"state": st["name"], "clip": None})
            break
        infos = [cinfo(i) for i in st["clips"]]
        ci = infos[0]
        chain.append({"state": st["name"], "stateSpeed": round(st["speed"], 4),
                      "clips": [{"clip": c["clip"], "length": c["length"], "events": c["events"]} for c in infos]})
        sspeed = st["speed"] if st["speed"] > 0 else 1.0
        # the transition's offset (normalized) is where the destination clip starts playing
        last_t = offset * ci["length"]
        for e in sorted([e for e in ci["events"] if e["t"] >= last_t], key=lambda e: e["t"]):
            t_real += (e["t"] - last_t) / (sspeed * anim_speed)
            last_t = e["t"]
            if e["fn"] == "Speed" and e["float"] > 0:
                anim_speed = e["float"]
            timeline.append({"t_real": round(t_real, 3), "state": st["name"], "clip": ci["clip"], "fn": e["fn"],
                             "param": e["obj"] or e["str"] or (e["float"] if e["float"] else (e["int"] or None))})
            if e["fn"] in TRIGGER_FUNCS and found is None:
                found = round(t_real, 3)
        end_real = t_real + (ci["length"] - last_t) / (sspeed * anim_speed)
        timeline.append({"t_real": round(end_real, 3), "state": st["name"], "clip": ci["clip"], "fn": "<clip end>", "param": None})
        nxt = [t for t in st["trans"] if t["hasExit"] and not t["conds"]]
        if not nxt:
            break
        t0 = nxt[0]
        # once the hit was found, only keep following into further states with the same tag
        # (e.g. "Double First" -> "Double Second"), not back into locomotion
        if found is not None and states[t0["dest"]]["tag"] != st["tag"]:
            break
        ex = t0["exit"] * ci["length"]
        t_real += (ex - last_t) / (sspeed * anim_speed)
        idx = t0["dest"]
        offset = t0["offset"]
    note = ""
    if found is None:
        note = "no OnAttackTrigger/Hit event found in chain"
    elif chain and len(chain[-1].get("clips") or []) > 1:
        note = "blend tree with several clips; timing from first clip"
    return {"chain": chain, "timeline": timeline, "windup_s": found, "note": note}

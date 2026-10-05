using System;
using Forewarned.Core;
using Forewarned.Core.Model;
using HarmonyLib;
using UnityEngine;

namespace Forewarned.Patches
{
    /// <summary>
    /// PLAN.md §3, §9: the warning signal. SetTrigger is a routed RPC to everybody (decomp
    /// ZSyncAnimation.cs:148-151, 217-220), so this postfix runs on every client that has the boss loaded,
    /// and synchronously inside Attack.Start on the owner.
    /// </summary>
    [HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger))]
    internal static class AnimTriggerPatch
    {
        private static void Postfix(ZSyncAnimation __instance, string name)
        {
            try
            {
                if (!Runtime.Ready || Player.m_localPlayer == null)
                    return;
                Character c = __instance.GetComponent<Character>();
                TrackedBoss t = BossWatch.Find(c);
                if (t == null)
                    return;
                var e = new TriggerEvent { BossId = t.Id, Prefab = t.Prefab, Trigger = name, Time = Time.time };
                TriggerKind kind = Runtime.Engine.OnTrigger(e, BossWatch.SceneFor(c), BossWatch.FightActive());
                if (kind == TriggerKind.Unmapped && PluginConfig.LogUnmappedTriggers.Value)
                    UnmappedLog.Note(t.Prefab, name);
                if (Runtime.Engine.Recent.Count > 0 && Runtime.Engine.Recent[0].Trigger == name)
                    Runtime.Debug("Forewarned: " + t.Prefab + " " + name + " -> " + Runtime.Engine.Recent[0].Outcome);
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("RPC_SetTrigger", e);
            }
        }
    }

    /// <summary>Logs each unknown (prefab, trigger) once.</summary>
    internal static class UnmappedLog
    {
        private static readonly System.Collections.Generic.HashSet<string> Seen = new System.Collections.Generic.HashSet<string>();

        public static void Note(string prefab, string trigger)
        {
            if (Seen.Add(prefab + " " + trigger))
                ForewarnedPlugin.Log.LogInfo("Forewarned unmapped trigger: " + prefab + " " + trigger);
        }
    }
}

using System;
using System.Globalization;
using Forewarned.Core;
using HarmonyLib;
using UnityEngine;

namespace Forewarned.Patches
{
    /// <summary>
    /// The damage moment: the attack clip's Hit / OnAttackTrigger animation event, which fires on every
    /// client's animator (decomp CharacterAnimEvent.cs:248-256). Ends the warning on time and teaches the
    /// tracker the real wind-up.
    /// </summary>
    [HarmonyPatch(typeof(CharacterAnimEvent))]
    internal static class AnimHitPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CharacterAnimEvent.Hit))]
        private static void HitPostfix(CharacterAnimEvent __instance) => OnHit(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CharacterAnimEvent.OnAttackTrigger))]
        private static void TriggerPostfix(CharacterAnimEvent __instance) => OnHit(__instance);

        private static void OnHit(CharacterAnimEvent ev)
        {
            try
            {
                if (!Runtime.Ready || Player.m_localPlayer == null)
                    return;
                TrackedBoss t = BossWatch.Find(ev.m_character);
                if (t == null)
                    return;
                float now = Time.time;
                float? windUp = Runtime.Engine.OnHit(t.Id, now);
                string line = "Forewarned t=" + now.ToString("F2", CultureInfo.InvariantCulture) + " " + t.Prefab + " hit";
                if (windUp.HasValue)
                    line += " after " + windUp.Value.ToString("F2", CultureInfo.InvariantCulture) + "s";
                Runtime.Debug(line);
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("CharacterAnimEvent hit", e);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Forewarned.Core;
using Forewarned.Core.Model.Bosses;
using HarmonyLib;

namespace Forewarned
{
    /// <summary>
    /// Boss attack warnings in the style of Deadly Boss Mods. Reads the boss's animator triggers as
    /// the game sends them and draws warnings; never changes game state. Purely client-side.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class ForewarnedPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.forewarned";
        public const string PluginName = "Forewarned";
        public const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Bind(Config, BossList.All);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ForewarnedPlugin).Assembly);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        private void Update()
        {
            try
            {
                Runtime.Tick();
            }
            catch (Exception e)
            {
                WarnOnce("Runtime.Tick", e);
            }
        }

        /// <summary>Log an exception once per key, so a broken hook can't flood the log every frame.</summary>
        internal static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
                Log.LogWarning(key + ": " + e);
        }
    }
}

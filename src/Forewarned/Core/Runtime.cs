using System.IO;
using System.Reflection;
using BepInEx;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Forewarned.UI;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>Owns the engine and runs the per-frame tick. Static: one game, one engine.</summary>
    internal static class Runtime
    {
        public static Engine Engine;
        private static bool _hadPlayer;

        public static bool Ready => Engine != null;

        public static void Tick()
        {
            if (!Ready)
                TryInit();
            if (!Ready)
                return;
            if (Player.m_localPlayer == null)
            {
                // Between death and respawn the world (and the fight) is still there: only the
                // dead player's own board and announcer should go, not every boss being tracked.
                if (ZNet.instance != null)
                {
                    Engine.Board.Clear();
                    Engine.Announcer.Clear();
                    DemoSequence.Clear();
                }
                else if (_hadPlayer)
                {
                    Engine.Clear();
                    BossWatch.Clear();
                    DemoSequence.Clear();
                    _hadPlayer = false;
                }
                return;
            }
            _hadPlayer = true;
            float now = Time.time;
            // Built before the ticks below, so an exception in a tick can't stop the HUD being built.
            WarningHud.Ensure();
            AnnounceHud.Ensure();
            EdgeFlash.Ensure();
            AlertSounds.Ensure();
            GroundMarkers.Ensure();
            BossWatch.Tick(now);
            Engine.Tick(now);
            DemoSequence.Tick(now);
        }

        public static void Debug(string line)
        {
            if (PluginConfig.Verbose.Value)
                ForewarnedPlugin.Log.LogInfo(line);
        }

        /// <summary>Localization.instance initialises itself on first access, so this runs on the first
        /// frame and fixes the language for the session: a language change needs a restart.</summary>
        private static void TryInit()
        {
            if (Localization.instance == null)
                return;
            Translations english = Translations.Parse(ReadResource("Forewarned.English.txt"));
            string language = Localization.instance.GetSelectedLanguage();
            string custom = Path.Combine(Path.Combine(Path.Combine(Paths.ConfigPath, "Forewarned"), "translations"), language + ".txt");
            Translations tr = File.Exists(custom) ? Translations.Parse(File.ReadAllText(custom)).WithFallback(english) : english;
            Engine = new Engine(new ModuleRegistry(BossList.All), tr, token => Localization.instance.Localize(token), new ConfigSettings());
            ForewarnedPlugin.Log.LogInfo("Forewarned ready: " + Engine.Registry.Modules.Count + " boss modules, language " + language +
                (File.Exists(custom) ? " (custom strings from " + custom + ")" : ""));
        }

        private static string ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    ForewarnedPlugin.Log.LogError("Missing embedded resource " + name);
                    return "";
                }
                using (var r = new StreamReader(s))
                    return r.ReadToEnd();
            }
        }
    }
}

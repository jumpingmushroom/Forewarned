using System.IO;
using System.Reflection;
using BepInEx;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
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
                if (_hadPlayer)
                {
                    Engine.Clear();
                    BossWatch.Clear();
                    _hadPlayer = false;
                }
                return;
            }
            _hadPlayer = true;
            float now = Time.time;
            BossWatch.Tick(now);
            Engine.Tick(now);
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

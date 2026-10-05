using System.Collections.Generic;
using BepInEx.Configuration;
using Forewarned.Core;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned
{
    /// <summary>
    /// PLAN.md §11.5: every option in F1. "00 General", then a section per boss in game order with an
    /// Enabled switch, pull and phase announces, and three rows per ability (warning, sound, visual)
    /// plus advanced level and always-warn rows. Abilities whose default level is None (no hit) get no rows.
    /// </summary>
    public static class PluginConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> OnlyDuringBossFight;
        public static ConfigEntry<bool> LogUnmappedTriggers;
        public static ConfigEntry<bool> Verbose;

        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> WarningOffsetY;
        public static ConfigEntry<float> AnnounceOffsetY;
        public static ConfigEntry<bool> ShowCountdownBar;
        public static ConfigEntry<bool> ShowSeconds;
        public static ConfigEntry<bool> EdgeFlash;
        public static ConfigEntry<bool> GroundMarkers;
        public static ConfigEntry<bool> PathArrow;
        public static ConfigEntry<float> MarkerFillOpacity;
        public static ConfigEntry<Color> DangerColor;
        public static ConfigEntry<Color> CautionColor;
        public static ConfigEntry<Color> InfoColor;
        public static ConfigEntry<float> Volume;

        private sealed class BossRows
        {
            public ConfigEntry<bool> Enabled;
            public ConfigEntry<bool> Announces;
        }

        private sealed class AbilityRows
        {
            public ConfigEntry<bool> Warn;
            public ConfigEntry<bool> Sound;
            public ConfigEntry<bool> Visual;
            public ConfigEntry<Level> Level;
            public ConfigEntry<bool> AlwaysWarn;
        }

        private static readonly Dictionary<BossModule, BossRows> Bosses = new Dictionary<BossModule, BossRows>();
        private static readonly Dictionary<AbilitySpec, AbilityRows> Abilities = new Dictionary<AbilitySpec, AbilityRows>();

        private static ConfigurationManagerAttributes Attr(int order, bool advanced = false) =>
            new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced };

        public static void Bind(ConfigFile cfg, IEnumerable<BossModule> modules)
        {
            const string general = "00 General";
            Enabled = cfg.Bind(general, "Enabled", true,
                new ConfigDescription("Master switch for all boss warnings.", null, Attr(100)));
            OnlyDuringBossFight = cfg.Bind(general, "OnlyDuringBossFight", true,
                new ConfigDescription("Warn only while a boss within 100 m is alerted (its health bar is up).", null, Attr(99)));
            LogUnmappedTriggers = cfg.Bind(general, "LogUnmappedTriggers", false,
                new ConfigDescription("Log boss animation triggers no module knows, once each, to catch attacks renamed by a game update.", null, Attr(10, true)));
            Verbose = cfg.Bind(general, "Verbose", false,
                new ConfigDescription("Log every boss trigger, hit and decision. Noisy.", null, Attr(9, true)));

            const string display = "01 Display";
            Scale = cfg.Bind(display, "Scale", 1f,
                new ConfigDescription("Size of the warnings and announce lines.", new AcceptableValueRange<float>(0.5f, 2.5f), Attr(100)));
            WarningOffsetY = cfg.Bind(display, "WarningOffsetY", 120f,
                new ConfigDescription("Height of the special warnings above the screen centre, in HUD pixels.", new AcceptableValueRange<float>(-400f, 600f), Attr(99)));
            AnnounceOffsetY = cfg.Bind(display, "AnnounceOffsetY", 150f,
                new ConfigDescription("Distance of the announce lines below the top of the screen, in HUD pixels (clear of the boss health bar).", new AcceptableValueRange<float>(0f, 800f), Attr(98)));
            ShowCountdownBar = cfg.Bind(display, "ShowCountdownBar", true,
                new ConfigDescription("A bar under each warning that runs out when the attack lands.", null, Attr(97)));
            ShowSeconds = cfg.Bind(display, "ShowSeconds", true,
                new ConfigDescription("Seconds left next to the bar.", null, Attr(96)));
            EdgeFlash = cfg.Bind(display, "EdgeFlash", true,
                new ConfigDescription("One pulse at the screen edges when a Danger warning appears.", null, Attr(95)));
            GroundMarkers = cfg.Bind(display, "GroundMarkers", true,
                new ConfigDescription("Draw attack areas on the ground (master switch for every ability's visual).", null, Attr(94)));
            PathArrow = cfg.Bind(display, "PathArrow", true,
                new ConfigDescription("An arrow at your feet showing the way out of the attack.", null, Attr(93)));
            MarkerFillOpacity = cfg.Bind(display, "MarkerFillOpacity", 0.25f,
                new ConfigDescription("How solid the area fill is (the outline stays strong).", new AcceptableValueRange<float>(0f, 1f), Attr(92)));
            DangerColor = cfg.Bind(display, "DangerColor", Hex("#FF5A36"),
                new ConfigDescription("Colour of Danger warnings and markers.", null, Attr(80, true)));
            CautionColor = cfg.Bind(display, "CautionColor", Hex("#FFD23F"),
                new ConfigDescription("Colour of Caution warnings and markers.", null, Attr(79, true)));
            InfoColor = cfg.Bind(display, "InfoColor", Hex("#7EC8FF"),
                new ConfigDescription("Colour of announce lines.", null, Attr(78, true)));

            Volume = cfg.Bind("02 Sounds", "Volume", 0.7f,
                new ConfigDescription("Alert volume (0 to 1), scaled by the game's master volume but not its effects slider.", new AcceptableValueRange<float>(0f, 1f), Attr(100)));
            cfg.Bind("02 Sounds", "Test Danger", "",
                new ConfigDescription("Show a sample Danger warning with its horn and flash.", null,
                    new ConfigurationManagerAttributes { Order = 90, HideDefaultButton = true, CustomDrawer = e => TestButton(Level.Danger) }));
            cfg.Bind("02 Sounds", "Test Caution", "",
                new ConfigDescription("Show a sample Caution warning with its chime.", null,
                    new ConfigurationManagerAttributes { Order = 89, HideDefaultButton = true, CustomDrawer = e => TestButton(Level.Caution) }));

            foreach (BossModule m in modules)
                BindBoss(cfg, m);
        }

        private static void BindBoss(ConfigFile cfg, BossModule m)
        {
            string section = m.Order.ToString("00") + " " + m.DisplayName;
            Bosses[m] = new BossRows
            {
                Enabled = cfg.Bind(section, "Enabled", true,
                    new ConfigDescription("Warnings for " + m.DisplayName + ".", null, Attr(1000))),
                Announces = cfg.Bind(section, "Pull and phase announces", true,
                    new ConfigDescription("Announce lines when the fight starts and at health thresholds.", null, Attr(999)))
            };

            int order = 990;
            foreach (AbilitySpec a in m.Abilities)
            {
                if (a.DefaultLevel == Level.None)
                    continue;
                string what = m.DisplayName + "'s " + a.Name.ToLowerInvariant();
                Abilities[a] = new AbilityRows
                {
                    Warn = cfg.Bind(section, a.Name + ": warning", a.DefaultOn,
                        new ConfigDescription("Warn when " + what + " starts (" + a.DefaultLevel + " by default).", null, Attr(order))),
                    Sound = cfg.Bind(section, a.Name + ": sound", DefaultSettings.DefaultSound(a),
                        new ConfigDescription("Play the alert sound with this warning.", null, Attr(order - 1))),
                    Visual = cfg.Bind(section, a.Name + ": visual", DefaultSettings.DefaultVisual(a),
                        new ConfigDescription("Draw the danger area and the way out on the ground.", null, Attr(order - 2))),
                    Level = cfg.Bind(section, a.Name + ": level", a.DefaultLevel,
                        new ConfigDescription("Urgency: Danger (big warning, horn, flash), Caution (smaller, chime), Info (announce line only).", null, Attr(order - 3, true))),
                    AlwaysWarn = cfg.Bind(section, a.Name + ": always warn", false,
                        new ConfigDescription("Warn even when you're clear of the attack.", null, Attr(order - 4, true)))
                };
                order -= 10;
            }
        }

        public static bool BossEnabled(BossModule m)
        {
            BossRows r;
            return !Bosses.TryGetValue(m, out r) || r.Enabled.Value;
        }

        public static bool Announces(BossModule m)
        {
            BossRows r;
            return !Bosses.TryGetValue(m, out r) || r.Announces.Value;
        }

        public static bool Warn(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Warn.Value : a.DefaultOn;
        }

        public static bool Sound(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Sound.Value : DefaultSettings.DefaultSound(a);
        }

        public static bool Visual(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Visual.Value : DefaultSettings.DefaultVisual(a);
        }

        public static Level LevelOf(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Level.Value : a.DefaultLevel;
        }

        public static bool AlwaysWarn(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) && r.AlwaysWarn.Value;
        }

        private static void TestButton(Level level)
        {
            if (GUILayout.Button("Show", GUILayout.ExpandWidth(true)))
                DemoSequence.Test(level);
        }

        private static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public static Color ColorFor(Level level)
        {
            switch (level)
            {
                case Level.Danger: return DangerColor.Value;
                case Level.Caution: return CautionColor.Value;
                default: return InfoColor.Value;
            }
        }
    }
}

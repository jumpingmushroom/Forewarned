using System;
using System.Globalization;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>"forewarned": tracked bosses, what is showing, the last triggers and their verdicts, and each
    /// ability's live vs offline numbers with the learned wind-up. Mirrored to the BepInEx log.</summary>
    internal static class ForewarnedConsole
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("forewarned", "Forewarned: tracked bosses, recent boss triggers and attack numbers",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    try
                    {
                        Report(args.Context);
                    }
                    catch (Exception e)
                    {
                        ForewarnedPlugin.WarnOnce("forewarned console", e);
                    }
                });
        }

        private static void Say(Terminal ctx, string line)
        {
            if (ctx != null)
                ctx.AddString(line);
            ForewarnedPlugin.Log.LogInfo(line);
        }

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string F(float? v) => v.HasValue ? F(v.Value) : "-";

        private static void Report(Terminal ctx)
        {
            Engine engine = Runtime.Engine;
            if (engine == null || Player.m_localPlayer == null)
            {
                Say(ctx, "Forewarned: load into a world first.");
                return;
            }
            float now = Time.time;
            Say(ctx, "Forewarned: enabled=" + PluginConfig.Enabled.Value + " fight=" + BossWatch.FightActive() + " t=" + F(now));

            foreach (TrackedBoss t in BossWatch.All)
            {
                float dist = t.Character != null ? Vector3.Distance(t.Character.transform.position, Player.m_localPlayer.transform.position) : -1f;
                Say(ctx, "  boss " + t.Prefab + " #" + t.Id + " hp " + F(t.Health * 100f) + "% alerted " + t.Alerted +
                    " flying " + t.Flying + " " + F(dist) + "m");
                BossModule m = engine.Registry.ModuleFor(t.Prefab);
                foreach (AbilitySpec a in m.Abilities)
                {
                    AbilityNumbers live = engine.LiveNumbers(t.Prefab, a.Id);
                    string learned = "";
                    foreach (string trigger in a.Triggers)
                    {
                        float? w = engine.Tracker.LearnedWindUp(t.Prefab, trigger);
                        if (w.HasValue)
                            learned += " " + trigger + " " + F(w) + "s x" + engine.Tracker.SampleCount(t.Prefab, trigger);
                    }
                    Say(ctx, "    " + a.Id + ": wind-up " + F(a.WindUp) + "s" + (learned.Length > 0 ? " learned" + learned : "") +
                        " | cd " + F(a.Cooldown) + "/" + F(live?.Cooldown) +
                        " range " + F(a.AiRange) + "/" + F(live?.AiRange) +
                        " hp " + F(a.HpMin) + "-" + F(a.HpMax) + "/" + F(live?.HpMin) + "-" + F(live?.HpMax) +
                        " size r " + F(a.Shape.Radius) + "/" + F(live?.Radius) + " len " + F(a.Shape.Range) + "/" + F(live?.Range) +
                        " angle " + F(a.Shape.Angle) + "/" + F(live?.Angle));
                }
            }

            foreach (Warning w in engine.Board.Active)
                Say(ctx, "  showing " + w.Level + " " + w.Title + " / " + w.Action + " hit in " + F(WarningBoard.Remaining(w, now)) + "s");
            foreach (Announce a in engine.Announcer.Lines)
                Say(ctx, "  announce " + a.Text);

            Say(ctx, "  last triggers (newest first):");
            foreach (TriggerRecord r in engine.Recent)
                Say(ctx, "    " + F(r.Time) + "s " + r.Prefab + " " + r.Trigger + " -> " + r.Outcome);
        }
    }
}

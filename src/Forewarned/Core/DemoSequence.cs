using System;
using System.Collections.Generic;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>
    /// Sample warnings for previews and screenshots, around an imaginary boss 8 m in front of the player
    /// and facing them. "Test" shows one level at once; "demo" plays a short Fader-like sequence.
    /// The fake boss id (-1) is never tracked, so markers use the pose saved in each warning.
    /// </summary>
    internal static class DemoSequence
    {
        private const long FakeBoss = -1;

        private sealed class Step
        {
            public float At;
            public Action<float> Run;
        }

        private static readonly List<Step> Steps = new List<Step>();
        private static float _start = -1f;

        public static void Test(Level level)
        {
            if (Runtime.Engine == null || Player.m_localPlayer == null)
                return;
            float now = Time.time;
            switch (level)
            {
                case Level.Danger:
                    Offer("FLAME BREATH", "Get behind Fader", Level.Danger, Shape.Line(39.45f, 3f), Response.GetBehind, 2.34f, now);
                    break;
                case Level.Caution:
                    Offer("CLAW", "Parry or roll", Level.Caution, Shape.Cone(10f, 65f), Response.Parry, 1.24f, now);
                    break;
                default:
                    Runtime.Engine.Announcer.Add("demo.info", "Charred Warriors incoming", now);
                    break;
            }
        }

        public static void Start()
        {
            if (Runtime.Engine == null || Player.m_localPlayer == null)
                return;
            Steps.Clear();
            Steps.Add(new Step { At = 0f, Run = now => Runtime.Engine.Announcer.Add("demo.pull", "Fader engaged", now) });
            Steps.Add(new Step { At = 0.5f, Run = now => Offer("FLAME BREATH", "Get behind Fader", Level.Danger, Shape.Line(39.45f, 3f), Response.GetBehind, 2.34f, now) });
            Steps.Add(new Step { At = 1.5f, Run = now => Offer("CLAW", "Parry or roll", Level.Caution, Shape.Cone(10f, 65f), Response.Parry, 1.24f, now) }); // overlaps the flame breath, so the demo shows stacking
            Steps.Add(new Step { At = 3.5f, Run = now => Runtime.Engine.Announcer.Add("demo.adds", "Charred Warriors incoming", now) });
            Steps.Add(new Step { At = 4f, Run = now => Offer("CLAW", "Parry or roll", Level.Caution, Shape.Cone(10f, 65f), Response.Parry, 1.24f, now) });
            Steps.Add(new Step { At = 6f, Run = now => Offer("METEORS", "Keep moving", Level.Danger, Shape.Circle(15f, 0f, Anchor.Target), Response.KeepMoving, 1.17f, now) });
            Steps.Add(new Step { At = 8.5f, Run = now => Offer("WALL OF FIRE", "Leave through the gap", Level.Danger, Shape.Ring(8f, 4f), Response.ExitRing, 1.43f, now) });
            Steps.Add(new Step { At = 11f, Run = now => Offer("SPIN", "Back out, 9 m", Level.Danger, Shape.Circle(8.5f), Response.LeaveArea, 1.3f, now) });
            _start = Time.time;
        }

        public static void Tick(float now)
        {
            if (_start < 0f)
                return;
            while (Steps.Count > 0 && now - _start >= Steps[0].At)
            {
                Step s = Steps[0];
                Steps.RemoveAt(0);
                s.Run(now);
            }
            if (Steps.Count == 0)
                _start = -1f;
        }

        public static void Clear()
        {
            Steps.Clear();
            _start = -1f;
        }

        private static void Offer(string title, string action, Level level, Shape shape, Response response, float windUp, float now)
        {
            Transform p = Player.m_localPlayer.transform;
            var me = new Vec2(p.position.x, p.position.z);
            Vec2 forward = new Vec2(p.forward.x, p.forward.z).Normalized;
            if (forward.Length == 0f)
                forward = new Vec2(0f, 1f);
            Vec2 boss = me + forward * 8f;
            Vec2 facing = forward * -1f;
            var w = new Warning
            {
                AbilityId = "demo." + title,
                BossId = FakeBoss,
                Level = level,
                Title = title,
                Action = action,
                Start = now,
                HitAt = now + windUp,
                Sound = level != Level.Info,
                Visual = level == Level.Danger,
                Shape = shape,
                Response = response,
                Origin = shape.Anchor == Anchor.Target ? me : boss,
                BossPos = boss,
                BossFacing = facing
            };
            Runtime.Engine.Board.Offer(w, now);
        }
    }
}

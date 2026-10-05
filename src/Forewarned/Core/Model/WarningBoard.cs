using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>One special warning: what the HUD draws above the crosshair, its bar and its ground marker.</summary>
    public sealed class Warning
    {
        public string AbilityId;
        public long BossId;
        public Level Level;
        public string Title;
        public string Action;
        public float Start;
        /// <summary>Predicted hit until HitSeen, then the real one.</summary>
        public float HitAt;
        public bool HitSeen;
        public bool Sound;
        public bool Visual;
        public Shape Shape;
        public Response Response;
        /// <summary>Where a Target-anchored shape lands; the boss's position at the trigger otherwise.</summary>
        public Vec2 Origin;
        /// <summary>The boss's position at the trigger. Boss-anchored shapes follow the live boss when it's
        /// tracked; this is the fallback, and the reference for directions such as "away from the boss".</summary>
        public Vec2 BossPos;
        /// <summary>The boss's facing at the trigger, a unit vector.</summary>
        public Vec2 BossFacing;
        /// <summary>Set by the board: Sound, unless the same ability sounded less than SoundGap ago.</summary>
        public bool PlaySound;
    }

    public enum OfferResult { Added, Replaced, Evicted, Dropped }

    /// <summary>PLAN.md §9 timing and stacking: two slots, newest on top, Caution never evicts Danger,
    /// an ability replaces its own line; hit + 0.5 s (at least 1.2 s), then a 0.3 s fade.</summary>
    public sealed class WarningBoard
    {
        public const int Slots = 2;
        public const float Linger = 0.5f;
        public const float MinShow = 1.2f;
        public const float FadeTime = 0.3f;
        public const float SoundGap = 1f;

        private readonly List<Warning> _active = new List<Warning>();
        private readonly Dictionary<string, float> _lastSound = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>Index 0 is the newest, drawn on top.</summary>
        public IReadOnlyList<Warning> Active => _active;

        public OfferResult Offer(Warning w, float now)
        {
            _active.RemoveAll(x => now >= EndAt(x)); // fading lines give up their slot
            OfferResult result = OfferResult.Added;
            int same = _active.FindIndex(x => x.AbilityId == w.AbilityId && x.BossId == w.BossId);
            if (same >= 0)
            {
                _active.RemoveAt(same);
                result = OfferResult.Replaced;
            }
            else if (_active.Count >= Slots)
            {
                // Lowest level first, the oldest among equals; never a line more urgent than w.
                int victim = -1;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    if (_active[i].Level > w.Level)
                        continue;
                    if (victim < 0 || _active[i].Level < _active[victim].Level)
                        victim = i;
                }
                if (victim < 0)
                    return OfferResult.Dropped;
                _active.RemoveAt(victim);
                result = OfferResult.Evicted;
            }

            float last;
            w.PlaySound = w.Sound && (!_lastSound.TryGetValue(w.AbilityId, out last) || now - last >= SoundGap);
            if (w.PlaySound)
                _lastSound[w.AbilityId] = now;
            _active.Insert(0, w);
            return result;
        }

        /// <summary>The boss's attack actually landed: the bar and the line end from here.</summary>
        public void Hit(string abilityId, long bossId, float time)
        {
            foreach (Warning w in _active)
                if (!w.HitSeen && w.AbilityId == abilityId && w.BossId == bossId)
                {
                    w.HitAt = time;
                    w.HitSeen = true;
                }
        }

        public void Tick(float now) => _active.RemoveAll(w => now >= EndAt(w) + FadeTime);

        public void Clear()
        {
            _active.Clear();
            _lastSound.Clear();
        }

        public static float EndAt(Warning w) => Math.Max(w.HitAt + Linger, w.Start + MinShow);

        public static float Alpha(Warning w, float now)
        {
            float end = EndAt(w);
            return now <= end ? 1f : Geometry.Clamp01(1f - (now - end) / FadeTime);
        }

        /// <summary>0 at the trigger, 1 at the hit: how far the countdown bar has drained.</summary>
        public static float Progress(Warning w, float now)
        {
            float span = w.HitAt - w.Start;
            return span <= 0f ? 1f : Geometry.Clamp01((now - w.Start) / span);
        }

        public static float Remaining(Warning w, float now) => Math.Max(0f, w.HitAt - now);
    }
}

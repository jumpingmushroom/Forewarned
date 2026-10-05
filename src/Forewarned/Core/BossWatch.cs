using System.Collections.Generic;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    internal sealed class TrackedBoss
    {
        public Character Character;
        public string Prefab;
        public long Id;
        public float Health = -1f;
        public bool Alerted;
        public bool Pulled;
        public bool Flying;
        public bool FlyingKnown;
        public bool LiveRead;
    }

    /// <summary>
    /// PLAN.md §11.3: every 0.2 s, finds the bosses (and later aspects) within 120 m that a module covers,
    /// and feeds their health, alert and flying state to the engine. Everything read here comes from the
    /// boss's network data, so it works on clients that don't own the boss.
    /// </summary>
    internal static class BossWatch
    {
        private const float Interval = 0.2f;
        private const float TrackRange = 120f;
        private const float FightRange = 100f;

        /// <summary>Character's animator bool "flying", as the owner writes it to the ZDO (decomp ZSyncAnimation.cs:159-170).</summary>
        private static readonly int FlyingKey = 438569 + ZSyncAnimation.GetHash("flying");

        private static readonly Dictionary<int, TrackedBoss> Tracked = new Dictionary<int, TrackedBoss>();
        private static readonly HashSet<int> NotBosses = new HashSet<int>();
        private static readonly List<int> Stale = new List<int>();
        private static float _next;

        public static IEnumerable<TrackedBoss> All => Tracked.Values;

        public static TrackedBoss Find(Character c)
        {
            TrackedBoss t;
            return c != null && Tracked.TryGetValue(c.GetInstanceID(), out t) ? t : null;
        }

        public static bool FightActive()
        {
            Player me = Player.m_localPlayer;
            if (me == null)
                return false;
            foreach (TrackedBoss t in Tracked.Values)
                if (t.Alerted && t.Character != null &&
                    Vector3.Distance(t.Character.transform.position, me.transform.position) <= FightRange)
                    return true;
            return false;
        }

        public static Scene SceneFor(Character boss)
        {
            Vector3 p = boss.transform.position;
            Vector3 f = boss.transform.forward;
            Vector3 m = Player.m_localPlayer.transform.position;
            var scene = new Scene { BossPos = new Vec2(p.x, p.z), BossFacing = new Vec2(f.x, f.z), Me = new Vec2(m.x, m.z) };
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != Player.m_localPlayer && !other.IsDead())
                    scene.Others.Add(new Vec2(other.transform.position.x, other.transform.position.z));
            return scene;
        }

        public static void Tick(float now)
        {
            if (now < _next)
                return;
            _next = now + Interval;
            Vector3 me = Player.m_localPlayer.transform.position;

            Stale.Clear();
            foreach (KeyValuePair<int, TrackedBoss> kv in Tracked)
            {
                Character c = kv.Value.Character;
                if (c == null || c.IsDead() || Vector3.Distance(c.transform.position, me) > TrackRange + 20f)
                    Stale.Add(kv.Key);
            }
            foreach (int key in Stale)
            {
                Runtime.Engine.Forget(Tracked[key].Id);
                Tracked.Remove(key);
            }

            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c is Player || c.IsDead())
                    continue;
                int key = c.GetInstanceID();
                if (NotBosses.Contains(key))
                    continue;
                TrackedBoss t;
                if (!Tracked.TryGetValue(key, out t))
                {
                    if (Vector3.Distance(c.transform.position, me) > TrackRange)
                        continue;
                    string prefab = Utils.GetPrefabName(c.gameObject);
                    if (!Runtime.Engine.Registry.Tracks(prefab))
                    {
                        NotBosses.Add(key);
                        continue;
                    }
                    t = new TrackedBoss { Character = c, Prefab = prefab, Id = key };
                    Tracked[key] = t;
                    Runtime.Debug("Forewarned: tracking " + prefab + " (" + key + ")");
                }
                Update(t, me, now);
            }
        }

        private static void Update(TrackedBoss t, Vector3 me, float now)
        {
            Character c = t.Character;
            if (!t.LiveRead)
                t.LiveRead = LiveData.Read(c as Humanoid, t.Prefab);

            float hp = c.GetHealthPercentage();
            if (hp != t.Health)
            {
                t.Health = hp;
                Runtime.Engine.OnHealth(t.Id, t.Prefab, hp, now);
            }

            BaseAI ai = c.GetBaseAI();
            t.Alerted = ai != null && ai.IsAlerted();
            if (!t.Alerted)
                t.Pulled = false;
            else if (!t.Pulled && Vector3.Distance(c.transform.position, me) <= FightRange)
            {
                t.Pulled = true;
                Runtime.Engine.OnPull(t.Id, t.Prefab, now);
            }

            bool flying = ReadFlying(c);
            if (!t.FlyingKnown || flying != t.Flying)
            {
                if (t.FlyingKnown)
                    Runtime.Engine.OnState(t.Id, t.Prefab, "flying", flying, now);
                t.Flying = flying;
                t.FlyingKnown = true;
            }
        }

        private static bool ReadFlying(Character c)
        {
            ZNetView nview = c.m_nview;
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null && zdo.GetInt(FlyingKey, 0) != 0;
        }

        public static void Clear()
        {
            Tracked.Clear();
            NotBosses.Clear();
            _next = 0f;
        }
    }
}

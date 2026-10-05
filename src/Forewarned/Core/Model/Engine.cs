using System;
using System.Collections.Generic;
using System.Globalization;

namespace Forewarned.Core.Model
{
    public sealed class TriggerEvent
    {
        public long BossId;
        public string Prefab;
        public string Trigger;
        public float Time;
    }

    /// <summary>One line of the console's trigger history.</summary>
    public sealed class TriggerRecord
    {
        public float Time;
        public string Prefab;
        public string Trigger;
        public string Outcome;
    }

    /// <summary>
    /// PLAN.md §11.1: the pure core. Game adapters feed it triggers, hits, health and state; it fills
    /// the warning board and the announcer, which the HUD draws. No Unity and no game types.
    /// </summary>
    public sealed class Engine
    {
        public const int RecentSize = 20;

        public readonly ModuleRegistry Registry;
        public readonly Tracker Tracker = new Tracker();
        public readonly WarningBoard Board = new WarningBoard();
        public readonly Announcer Announcer = new Announcer();
        public readonly PhaseTracker Phases = new PhaseTracker();

        private readonly Translations _tr;
        private readonly Func<string, string> _localize;
        private readonly IAbilitySettings _settings;
        private readonly List<TriggerRecord> _recent = new List<TriggerRecord>();
        private readonly Dictionary<string, AbilityNumbers> _live = new Dictionary<string, AbilityNumbers>(StringComparer.Ordinal);

        /// <param name="localize">Turns a game token ("$enemy_fader") into the player's language.</param>
        public Engine(ModuleRegistry registry, Translations tr, Func<string, string> localize, IAbilitySettings settings)
        {
            Registry = registry;
            _tr = tr ?? new Translations();
            _localize = localize;
            _settings = settings;
        }

        /// <summary>Newest first.</summary>
        public IReadOnlyList<TriggerRecord> Recent => _recent;

        /// <summary>Live numbers read from the boss's own item in game, for warnings that use that ability
        /// next. Null clears them, so the spec's offline numbers are used again.</summary>
        public void SetLiveNumbers(string prefab, string abilityId, AbilityNumbers numbers) =>
            _live[LiveKey(prefab, abilityId)] = numbers;

        public AbilityNumbers LiveNumbers(string prefab, string abilityId)
        {
            AbilityNumbers numbers;
            return _live.TryGetValue(LiveKey(prefab, abilityId), out numbers) ? numbers : null;
        }

        public TriggerKind OnTrigger(TriggerEvent e, Scene scene, bool fightActive)
        {
            AbilitySpec spec;
            TriggerKind kind = Registry.Classify(e.Prefab, e.Trigger, out spec);
            if (kind == TriggerKind.NotTracked)
                return kind;
            if (kind != TriggerKind.Mapped)
            {
                // An abort, stagger or unknown attack means the previous trigger's hit won't come.
                Tracker.Cancel(e.BossId);
                // Ignored triggers are routine noise (dodge, equip, ...); only Unmapped is worth a look.
                if (kind == TriggerKind.Unmapped)
                    Record(e, "unmapped");
                return kind;
            }
            Tracker.OnTrigger(e.BossId, e.Prefab, e.Trigger, spec.Id, e.Time);
            Record(e, Warn(e, Registry.ModuleFor(e.Prefab), spec, scene, LiveNumbers(e.Prefab, spec.Id), fightActive));
            return kind;
        }

        private string Warn(TriggerEvent e, BossModule module, AbilitySpec spec, Scene scene, AbilityNumbers live, bool fightActive)
        {
            if (!_settings.Enabled || !_settings.BossEnabled(module))
                return "off";
            if (_settings.OnlyDuringBossFight && !fightActive)
                return "no fight";
            Level level = _settings.LevelOf(spec);
            if (level == Level.None)
                return "none";
            if (!_settings.Warn(spec))
                return "off";

            ResolvedAbility a = ResolvedAbility.Resolve(spec, live, LearnedWindUp(e.Prefab, e.Trigger, spec));
            Vec2 origin;
            Verdict verdict = Relevance.Judge(a.Shape, a.AiRange, scene, out origin);
            Outcome outcome = Relevance.Decide(level, verdict, _settings.AlwaysWarn(spec));
            string where = verdict.ToString().ToLowerInvariant();
            string boss = BossName(module);

            if (outcome == Outcome.Nothing)
                return "nothing, " + where;
            if (outcome == Outcome.Announce)
            {
                // The action text names no ability ("Get behind Fader"); only show it for abilities
                // that are Info by design. One the player raised to Info in settings needs the
                // ability's name too, so it gets the "{boss}: {what}" line instead.
                string text = spec.DefaultLevel == Level.Info
                    ? Fill(_tr.Get(spec.ActionKey), boss, null, a.Shape.SafeMetres)
                    : Fill(_tr.Get("announce.elsewhere"), boss, AbilityName(spec), 0);
                Announcer.Add(spec.Id, text, e.Time);
                return "announce, " + where;
            }

            var w = new Warning
            {
                AbilityId = spec.Id,
                BossId = e.BossId,
                Level = level,
                Title = AbilityName(spec).ToUpperInvariant(),
                Action = Fill(_tr.Get(spec.ActionKey), boss, null, a.Shape.SafeMetres),
                Start = e.Time,
                HitAt = e.Time + a.WindUp,
                Sound = _settings.Sound(spec),
                Visual = _settings.Visual(spec),
                Shape = a.Shape,
                Response = spec.Response,
                Origin = origin
            };
            return "special " + Board.Offer(w, e.Time).ToString().ToLowerInvariant() + ", " + where;
        }

        /// <summary>The learned wind-up, trusted only once it has at least two samples and sits within
        /// ±50% of the spec's offline figure; otherwise null, so ResolvedAbility falls back to it.</summary>
        private float? LearnedWindUp(string prefab, string trigger, AbilitySpec spec)
        {
            float? learned = Tracker.LearnedWindUp(prefab, trigger);
            if (learned == null || Tracker.SampleCount(prefab, trigger) < 2)
                return null;
            return Math.Abs(learned.Value - spec.WindUp) <= spec.WindUp * 0.5f ? learned : null;
        }

        /// <returns>The paired wind-up (hit time minus trigger time), or null when nothing was paired.</returns>
        public float? OnHit(long bossId, float time)
        {
            string abilityId = Tracker.OnHit(bossId, time);
            if (abilityId == null)
                return null;
            Board.Hit(abilityId, bossId, time);
            return Tracker.LastWindUp;
        }

        public void OnHealth(long bossId, string prefab, float hp, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
            if (m == null)
                return;
            Tracker.SetHealth(bossId, hp);
            IList<Phase> crossed = Phases.Observe(bossId, m.Phases, hp);
            if (!AnnouncesFor(m))
                return;
            foreach (Phase p in crossed)
                Announcer.Add(p.Key, Fill(_tr.Get(p.Key), BossName(m), null, 0), now);
        }

        public void OnState(long bossId, string prefab, string state, bool value, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
            string key = m != null ? m.OnStateChanged(state, value) : null;
            if (key != null && AnnouncesFor(m))
                Announcer.Add(key, Fill(_tr.Get(key), BossName(m), null, 0), now);
        }

        public void OnPull(long bossId, string prefab, float now)
        {
            // A fresh pull is a fresh fight: a boss that wipes the group and heals back to full
            // should announce its thresholds again on the way back down.
            Phases.Forget(bossId);
            BossModule m = Registry.ModuleFor(prefab);
            // Re-seed at the boss's current health: without this, a re-pull that didn't fully heal
            // (or joining mid-fight) would treat the next health change as the first observation and
            // silently mark a threshold already below current health as crossed without announcing it.
            float? hp = Tracker.Health(bossId);
            if (m != null && hp.HasValue)
                Phases.Observe(bossId, m.Phases, hp.Value);
            if (m != null && AnnouncesFor(m))
                Announcer.Add("pull." + m.Key, Fill(_tr.Get("announce.pull"), BossName(m), null, 0), now);
        }

        public void Forget(long bossId)
        {
            Tracker.Forget(bossId);
            Phases.Forget(bossId);
        }

        public void Tick(float now)
        {
            Board.Tick(now);
            Announcer.Tick(now);
        }

        public void Clear()
        {
            Tracker.Clear();
            Board.Clear();
            Announcer.Clear();
            Phases.Clear();
            _recent.Clear();
            _live.Clear();
        }

        /// <summary>The boss's name in the player's language, or the module's English name if the game has none.</summary>
        public string BossName(BossModule m)
        {
            string name = _localize != null ? _localize(m.NameToken) : null;
            return TextCheck.IsClean(name) ? name : m.DisplayName;
        }

        private bool AnnouncesFor(BossModule m) => _settings.Enabled && _settings.BossEnabled(m) && _settings.Announces(m);

        private string AbilityName(AbilitySpec spec)
        {
            string name = _tr.Get(spec.NameKey);
            return TextCheck.IsClean(name) ? name : spec.Name;
        }

        private static string Fill(string template, string boss, string what, int metres)
        {
            if (template == null)
                return "";
            return template
                .Replace("{boss}", boss ?? "")
                .Replace("{what}", what ?? "")
                .Replace("{m}", metres.ToString(CultureInfo.InvariantCulture));
        }

        private static string LiveKey(string prefab, string abilityId) => prefab + "\n" + abilityId;

        private void Record(TriggerEvent e, string outcome)
        {
            _recent.Insert(0, new TriggerRecord { Time = e.Time, Prefab = e.Prefab, Trigger = e.Trigger, Outcome = outcome });
            if (_recent.Count > RecentSize)
                _recent.RemoveAt(_recent.Count - 1);
        }
    }
}

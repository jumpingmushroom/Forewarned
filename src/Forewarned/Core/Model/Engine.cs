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

        public TriggerKind OnTrigger(TriggerEvent e, Scene scene, AbilityNumbers live, bool fightActive)
        {
            AbilitySpec spec;
            TriggerKind kind = Registry.Classify(e.Prefab, e.Trigger, out spec);
            if (kind == TriggerKind.NotTracked)
                return kind;
            if (kind != TriggerKind.Mapped)
            {
                Record(e, kind == TriggerKind.Ignored ? "ignored" : "unmapped");
                return kind;
            }
            Tracker.OnTrigger(e.BossId, e.Prefab, e.Trigger, spec.Id, e.Time);
            Record(e, Warn(e, Registry.ModuleFor(e.Prefab), spec, scene, live, fightActive));
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

            ResolvedAbility a = ResolvedAbility.Resolve(spec, live, Tracker.LearnedWindUp(e.Prefab, e.Trigger));
            Vec2 origin;
            Verdict verdict = Relevance.Judge(a.Shape, a.AiRange, scene, out origin);
            Outcome outcome = Relevance.Decide(level, verdict, _settings.AlwaysWarn(spec));
            string where = verdict.ToString().ToLowerInvariant();
            string boss = BossName(module);

            if (outcome == Outcome.Nothing)
                return "nothing, " + where;
            if (outcome == Outcome.Announce)
            {
                string text = level == Level.Info
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

        public void OnHit(long bossId, float time)
        {
            string abilityId = Tracker.OnHit(bossId, time);
            if (abilityId != null)
                Board.Hit(abilityId, bossId, time);
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

        public void OnPull(string prefab, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
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

        private void Record(TriggerEvent e, string outcome)
        {
            _recent.Insert(0, new TriggerRecord { Time = e.Time, Prefab = e.Prefab, Trigger = e.Trigger, Outcome = outcome });
            if (_recent.Count > RecentSize)
                _recent.RemoveAt(_recent.Count - 1);
        }
    }
}

using System.Collections.Generic;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class EngineTests
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "$enemy_fader", "Fader" }, { "$enemy_dragon", "Moder" }
        };

        private sealed class Settings : IAbilitySettings
        {
            private readonly DefaultSettings _d = new DefaultSettings();
            public HashSet<string> On = new HashSet<string>();
            /// <summary>Id of an ability the player has forced to Info in settings, overriding its DefaultLevel.</summary>
            public string Info;
            public bool Enabled => true;
            public bool OnlyDuringBossFight => true;
            public bool BossEnabled(BossModule m) => true;
            public bool Announces(BossModule m) => true;
            public bool Warn(AbilitySpec a) => On.Contains(a.Id) || _d.Warn(a);
            public bool Sound(AbilitySpec a) => _d.Sound(a);
            public bool Visual(AbilitySpec a) => _d.Visual(a);
            public Level LevelOf(AbilitySpec a) => a.Id == Info ? Level.Info : _d.LevelOf(a);
            public bool AlwaysWarn(AbilitySpec a) => false;
        }

        private static Engine Make(Settings s = null) =>
            new Engine(new ModuleRegistry(BossList.All), DataFilesTests.English(), t => Names.TryGetValue(t, out var n) ? n : t, s ?? new Settings());

        private static Scene At(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        private static TriggerEvent Fader(string trigger, float time) =>
            new TriggerEvent { BossId = 7, Prefab = "Fader", Trigger = trigger, Time = time };

        [Fact]
        public void InFrontOfTheFlameBreathYouGetTheSpecialWarning()
        {
            Engine e = Make();
            Assert.Equal(TriggerKind.Mapped, e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true));
            Warning w = Assert.Single(e.Board.Active);
            Assert.Equal("FLAME BREATH", w.Title);
            Assert.Equal("Get behind Fader", w.Action);
            Assert.Equal(Level.Danger, w.Level);
            Assert.Equal(12.34f, w.HitAt, 3);
            Assert.True(w.Sound);
            Assert.True(w.Visual);
            Assert.True(w.PlaySound);
            Assert.Equal(Response.GetBehind, w.Response);
            Assert.StartsWith("special added", e.Recent[0].Outcome);
        }

        [Fact]
        public void BehindTheBossItIsOnlyAnAnnounce()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, -10f), true);
            Assert.Empty(e.Board.Active);
            Assert.Equal("Fader: Flame breath", Assert.Single(e.Announcer.Lines).Text);
        }

        [Fact]
        public void NothingOutsideABossFight()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), false);
            Assert.Empty(e.Board.Active);
            Assert.Equal("no fight", e.Recent[0].Outcome);
        }

        [Fact]
        public void CautionIsOffByDefaultButCanBeTurnedOn()
        {
            Engine off = Make();
            off.OnTrigger(Fader("attack_bite", 10f), At(0f, 5f), true);
            Assert.Empty(off.Board.Active);
            Assert.Equal("off", off.Recent[0].Outcome);

            var s = new Settings();
            s.On.Add("fader.bite");
            Engine on = Make(s);
            on.OnTrigger(Fader("attack_bite", 10f), At(0f, 5f), true);
            Assert.Equal("BITE", Assert.Single(on.Board.Active).Title);
        }

        [Fact]
        public void InfoAbilitiesAnnounceTheirAction()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_roar", 10f), At(0f, 50f), true);
            Assert.Equal("Charred Warriors incoming", Assert.Single(e.Announcer.Lines).Text);
        }

        [Fact]
        public void ANonInfoAbilityForcedToInfoAnnouncesTheElsewhereLineWithItsName()
        {
            var s = new Settings { Info = "fader.flamebreath" };
            Engine e = Make(s);
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, -10f), true);
            Assert.Equal("Fader: Flame breath", Assert.Single(e.Announcer.Lines).Text);
        }

        [Fact]
        public void DistancesComeFromTheShape()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_Spin", 10f), At(0f, 5f), true);
            Assert.Equal("Back out, 9 m", Assert.Single(e.Board.Active).Action);
            Engine live = Make();
            live.SetLiveNumbers("Fader", "fader.spin", new AbilityNumbers { Radius = 10f });
            live.OnTrigger(Fader("attack_Spin", 10f), At(0f, 5f), true);
            Assert.Equal("Back out, 10 m", Assert.Single(live.Board.Active).Action);
        }

        [Fact]
        public void UnknownTriggersAreRecordedAndOthersIgnored()
        {
            Engine e = Make();
            Assert.Equal(TriggerKind.Unmapped, e.OnTrigger(Fader("jump_forward", 10f), At(0f, 5f), true));
            Assert.Equal("unmapped", e.Recent[0].Outcome);
            Assert.Equal(TriggerKind.Ignored, e.OnTrigger(Fader("attack_abort", 11f), At(0f, 5f), true));
            Assert.Equal(TriggerKind.NotTracked, e.OnTrigger(new TriggerEvent { BossId = 9, Prefab = "Greydwarf", Trigger = "attack", Time = 12f }, At(0f, 5f), true));
            Assert.Single(e.Recent);
        }

        [Fact]
        public void TheRealHitEndsTheWarningAndTeachesTheNextOne()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true);
            e.OnHit(7, 12f);
            Assert.Equal(12f, e.Board.Active[0].HitAt, 3);
            Assert.True(e.Board.Active[0].HitSeen);
            e.Board.Clear();
            // One sample isn't enough to trust yet: the next warning still uses the spec's wind-up.
            e.OnTrigger(Fader("attack_flamebreath", 40f), At(0f, 10f), true);
            Assert.Equal(42.34f, e.Board.Active[0].HitAt, 3);
        }

        [Fact]
        public void AnAbortCancelsThePendingTriggerSoTheNextHitDoesNotPairWithIt()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true);
            e.OnTrigger(Fader("attack_abort", 10.5f), At(0f, 10f), true);
            e.OnHit(7, 12f);
            Assert.False(e.Board.Active[0].HitSeen);
        }

        [Fact]
        public void OneLearnedSampleIsNotUsedButTwoConsistentOnesAre()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true);
            e.OnHit(7, 12f);
            e.Board.Clear();
            e.OnTrigger(Fader("attack_flamebreath", 40f), At(0f, 10f), true);
            Assert.Equal(42.34f, e.Board.Active[0].HitAt, 3);
            e.OnHit(7, 42f);
            e.Board.Clear();
            e.OnTrigger(Fader("attack_flamebreath", 80f), At(0f, 10f), true);
            Assert.Equal(82f, e.Board.Active[0].HitAt, 3);
        }

        [Fact]
        public void ALearnedValueOutsideTheSanityBandIsIgnored()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true);
            e.OnHit(7, 15f); // 5 s: a long way outside flamebreath's ±50% band around 2.34 s
            e.Board.Clear();
            e.OnTrigger(Fader("attack_flamebreath", 20f), At(0f, 10f), true);
            e.OnHit(7, 25f);
            e.Board.Clear();
            e.OnTrigger(Fader("attack_flamebreath", 30f), At(0f, 10f), true);
            Assert.Equal(32.34f, e.Board.Active[0].HitAt, 3);
        }

        [Fact]
        public void PhasesAreAnnounced()
        {
            Engine e = Make();
            e.OnHealth(7, "Fader", 0.9f, 1f);
            Assert.Empty(e.Announcer.Lines);
            e.OnHealth(7, "Fader", 0.84f, 2f);
            Assert.Equal("Fader 85%: Fissure and Flame breath", Assert.Single(e.Announcer.Lines).Text);
            Assert.Equal(0.84f, e.Tracker.Health(7));
        }

        [Fact]
        public void ModerAnnouncesTakeoffAndThePull()
        {
            Engine e = Make();
            e.OnPull(3, "Dragon", 1f);
            e.OnState(3, "Dragon", "flying", true, 2f);
            Assert.Equal("Moder engaged", e.Announcer.Lines[0].Text);
            Assert.Equal("Moder takes off", e.Announcer.Lines[1].Text);
        }

        [Fact]
        public void OnPullResetsThePhasesSoAWipedBossAnnouncesThemAgain()
        {
            Engine e = Make();
            e.OnHealth(7, "Fader", 0.9f, 1f);
            e.OnHealth(7, "Fader", 0.84f, 2f);
            Assert.Equal(2f, Assert.Single(e.Announcer.Lines).Start);
            // BossWatch always reads the boss's health for this tick, and calls OnHealth if it
            // changed, before it calls OnPull: by the time a fresh pull fires, the boss having
            // healed back to 90% is already reflected in Tracker.Health, same as here.
            e.OnHealth(7, "Fader", 0.9f, 10f);
            e.OnPull(7, "Fader", 11f);
            e.OnHealth(7, "Fader", 0.84f, 12f);
            Assert.Equal(12f, e.Announcer.Lines[e.Announcer.Lines.Count - 1].Start);
        }

        [Fact]
        public void OnPullReseedsPhasesAtCurrentHealthSoARePullWithoutAFullHealStillAnnouncesTheNextThreshold()
        {
            Engine e = Make();
            e.OnHealth(7, "Fader", 0.9f, 1f);
            e.OnPull(7, "Fader", 2f);
            e.OnHealth(7, "Fader", 0.84f, 3f);
            Assert.Equal("Fader 85%: Fissure and Flame breath", e.Announcer.Lines[e.Announcer.Lines.Count - 1].Text);
        }

        [Fact]
        public void LevelNoneIsSilent()
        {
            Engine e = Make();
            e.OnTrigger(new TriggerEvent { BossId = 3, Prefab = "Dragon", Trigger = "attack_taunt", Time = 1f }, At(0f, 5f), true);
            Assert.Empty(e.Board.Active);
            Assert.Empty(e.Announcer.Lines);
            Assert.Equal("none", e.Recent[0].Outcome);
        }

        [Fact]
        public void RecentKeepsTheNewestTwenty()
        {
            Engine e = Make();
            for (int i = 0; i < 25; i++)
                e.OnTrigger(Fader("jump_forward", i), At(0f, 5f), true);
            Assert.Equal(Engine.RecentSize, e.Recent.Count);
            Assert.Equal(24f, e.Recent[0].Time);
        }

        [Fact]
        public void ForgetAndClear()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), true);
            e.OnHealth(7, "Fader", 0.5f, 10f);
            e.Forget(7);
            Assert.Null(e.Tracker.Health(7));
            e.Clear();
            Assert.Empty(e.Board.Active);
            Assert.Empty(e.Recent);
        }
    }
}

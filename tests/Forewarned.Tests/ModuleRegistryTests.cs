using System;
using System.Collections.Generic;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class ModuleRegistryTests
    {
        private sealed class TestModule : BossModule
        {
            private readonly string[] _prefabs;
            private readonly AbilitySpec[] _abilities;

            public TestModule(string[] prefabs, params AbilitySpec[] abilities)
            {
                _prefabs = prefabs;
                _abilities = abilities;
            }

            public override string Key => "test";
            public override string DisplayName => "Test";
            public override string NameToken => "$enemy_test";
            public override int Order => 3;
            public override string[] Prefabs => _prefabs;
            public override IReadOnlyList<AbilitySpec> Abilities => _abilities;
        }

        private static AbilitySpec Spec(string id, params string[] triggers) =>
            new AbilitySpec { Id = id, Name = id, ItemPrefab = id, Triggers = triggers, DefaultLevel = Level.Danger };

        private static ModuleRegistry Registry() =>
            new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader", "Aspect_Fader" }, Spec("fader.claw", "attack_ClawL", "attack_ClawR"), Spec("fader.meteors", "taunt"))
            });

        [Fact]
        public void MapsTriggersOfEveryPrefabTheModuleClaims()
        {
            ModuleRegistry r = Registry();
            AbilitySpec a;
            Assert.Equal(TriggerKind.Mapped, r.Classify("Fader", "attack_ClawR", out a));
            Assert.Equal("fader.claw", a.Id);
            Assert.Equal(TriggerKind.Mapped, r.Classify("Aspect_Fader", "taunt", out a));
            Assert.Equal("fader.meteors", a.Id);
            Assert.Same(r.ModuleFor("Fader"), r.ModuleFor("Aspect_Fader"));
            Assert.True(r.Tracks("Aspect_Fader"));
        }

        [Fact]
        public void SortsOutTheRest()
        {
            ModuleRegistry r = Registry();
            AbilitySpec a;
            Assert.Equal(TriggerKind.Ignored, r.Classify("Fader", "attack_abort", out a));
            Assert.Null(a);
            Assert.Equal(TriggerKind.Unmapped, r.Classify("Fader", "jump_forward", out a));
            Assert.Equal(TriggerKind.NotTracked, r.Classify("Greydwarf", "attack", out a));
            Assert.Null(r.ModuleFor("Greydwarf"));
            Assert.False(r.Tracks("Greydwarf"));
        }

        [Fact]
        public void RejectsDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader" }, Spec("a", "taunt"), Spec("b", "taunt"))
            }));
            Assert.Throws<ArgumentException>(() => new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader" }, Spec("a", "x")),
                new TestModule(new[] { "Fader" }, Spec("b", "y"))
            }));
        }

        [Fact]
        public void DefaultsAreEmpty()
        {
            var m = new TestModule(new[] { "X" });
            Assert.Empty(m.Phases);
            Assert.Null(m.OnStateChanged("flying", true));
        }
    }
}

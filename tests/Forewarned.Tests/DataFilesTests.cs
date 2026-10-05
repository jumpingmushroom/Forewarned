using System.IO;
using System.Linq;
using System.Reflection;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class DataFilesTests
    {
        internal static Translations English()
        {
            using (Stream s = typeof(DataFilesTests).Assembly.GetManifestResourceStream("Forewarned.English.txt"))
            using (var r = new StreamReader(s))
                return Translations.Parse(r.ReadToEnd());
        }

        private static void Clean(Translations tr, string key)
        {
            string text = tr.Get(key);
            Assert.True(text != null, "missing key " + key);
            Assert.True(TextCheck.IsClean(text.Replace("{boss}", "B").Replace("{what}", "W").Replace("{m}", "1")), "unclean " + key + " = " + text);
        }

        [Fact]
        public void TheRegistryBuildsFromEveryModule()
        {
            var r = new ModuleRegistry(BossList.All);
            Assert.Equal(3, r.Modules.Count);
        }

        [Fact]
        public void EveryAbilityIsComplete()
        {
            foreach (BossModule m in BossList.All)
                foreach (AbilitySpec a in m.Abilities)
                {
                    Assert.False(string.IsNullOrEmpty(a.ItemPrefab), a.Id);
                    Assert.NotEmpty(a.Triggers);
                    Assert.StartsWith(m.Key + ".", a.Id);
                    Assert.True(a.WindUp > 0f, a.Id);
                    if (a.DefaultLevel != Level.None)
                        Assert.False(string.IsNullOrEmpty(a.ActionKey), a.Id);
                    if (a.DefaultLevel == Level.Danger)
                        Assert.True(a.Shape.Kind != ShapeKind.None, a.Id + " is Danger but has no shape");
                    if (a.DefaultLevel == Level.Caution)
                        Assert.False(a.DefaultOn, a.Id + ": Caution is off by default (PLAN.md §9)");
                }
        }

        [Fact]
        public void EveryKeyHasCleanEnglish()
        {
            Translations tr = English();
            Clean(tr, "announce.pull");
            Clean(tr, "announce.elsewhere");
            foreach (BossModule m in BossList.All)
            {
                foreach (AbilitySpec a in m.Abilities)
                {
                    Clean(tr, a.NameKey);
                    if (a.ActionKey != null)
                        Clean(tr, a.ActionKey);
                }
                foreach (Phase p in m.Phases)
                    Clean(tr, p.Key);
                foreach (string key in new[] { m.OnStateChanged("flying", true), m.OnStateChanged("flying", false) })
                    if (key != null)
                        Clean(tr, key);
            }
        }

        [Fact]
        public void FaderMatchesTheResearch()
        {
            AbilitySpec breath = BossList.All.OfType<FaderModule>().Single().Abilities.Single(a => a.Id == "fader.flamebreath");
            Assert.Equal(2.34f, breath.WindUp);
            Assert.Equal(25f, breath.Cooldown);
            Assert.Equal(0.05f, breath.HpMin);
            Assert.Equal(0.85f, breath.HpMax);
            Assert.Equal(new[] { 0.85f, 0.55f, 0.35f, 0.25f }, new FaderModule().Phases.Select(p => p.At).ToArray());
        }

        [Fact]
        public void ModerAnnouncesFlying()
        {
            var moder = new ModerModule();
            Assert.Equal("moder.takeoff", moder.OnStateChanged("flying", true));
            Assert.Equal("moder.land", moder.OnStateChanged("flying", false));
            Assert.Null(moder.OnStateChanged("other", true));
        }
    }
}

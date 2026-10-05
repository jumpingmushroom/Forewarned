using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TrackerTests
    {
        private const string Fader = "Fader";
        private const string Breath = "attack_flamebreath";

        [Fact]
        public void AHitEndsThePendingTriggerAndTeachesTheWindUp()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12.3f));
            Assert.Equal(2.3f, t.LearnedWindUp(Fader, Breath).Value, 3);
            Assert.Equal(10f, t.LastUse(1, "fader.flamebreath"));
        }

        [Fact]
        public void HitsWithoutATriggerOrAfterTheFirstAreIgnored()
        {
            var t = new Tracker();
            Assert.Null(t.OnHit(1, 5f));
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            t.OnHit(1, 12f);
            Assert.Null(t.OnHit(1, 13f)); // second hit of a multi-hit attack
            Assert.Equal(1, t.SampleCount(Fader, Breath));
        }

        [Fact]
        public void TooSoonKeepsWaitingTooLateIsDropped()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Null(t.OnHit(1, 10.1f));
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12f));
            Assert.Equal(2f, t.LearnedWindUp(Fader, Breath).Value, 3);

            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 20f);
            Assert.Null(t.OnHit(1, 27f));
            Assert.Equal(1, t.SampleCount(Fader, Breath));
        }

        [Fact]
        public void OutliersDoNotMoveTheEstimate()
        {
            var t = new Tracker();
            float[] samples = { 2.3f, 2.35f, 2.25f, 5f };
            for (int i = 0; i < samples.Length; i++)
            {
                t.OnTrigger(1, Fader, Breath, "fader.flamebreath", i * 10f);
                t.OnHit(1, i * 10f + samples[i]);
            }
            Assert.Equal(2.3f, t.LearnedWindUp(Fader, Breath).Value, 3);
        }

        [Fact]
        public void KeepsOnlyTheLatestSamples()
        {
            var t = new Tracker();
            for (int i = 0; i < 12; i++)
            {
                t.OnTrigger(1, Fader, Breath, "fader.flamebreath", i * 10f);
                t.OnHit(1, i * 10f + (i < 3 ? 1f : 2f));
            }
            Assert.Equal(Tracker.MaxSamples, t.SampleCount(Fader, Breath));
            Assert.Equal(2f, t.LearnedWindUp(Fader, Breath).Value, 3);
        }

        [Fact]
        public void CancelDropsThePendingTriggerSoALaterHitFindsNothing()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            t.Cancel(1);
            Assert.Null(t.OnHit(1, 12f));
        }

        [Fact]
        public void BossesAreTrackedSeparately()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Null(t.OnHit(2, 12f));
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12f));
            t.SetHealth(1, 0.5f);
            Assert.Equal(0.5f, t.Health(1));
            Assert.Null(t.Health(2));
        }

        [Fact]
        public void ForgetClearsOneBossButKeepsWhatWasLearned()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            t.OnHit(1, 12f);
            t.SetHealth(1, 0.5f);
            t.Forget(1);
            Assert.Null(t.Health(1));
            Assert.Null(t.LastUse(1, "fader.flamebreath"));
            Assert.Equal(1, t.SampleCount(Fader, Breath));
            t.Clear();
            Assert.Null(t.LearnedWindUp(Fader, Breath));
        }
    }
}

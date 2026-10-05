using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class WarningBoardTests
    {
        private static Warning W(string id, Level level, float start, float windUp = 2f, bool sound = true) =>
            new Warning { AbilityId = id, BossId = 1, Level = level, Title = id, Start = start, HitAt = start + windUp, Sound = sound };

        [Fact]
        public void NewestGoesOnTop()
        {
            var b = new WarningBoard();
            Assert.Equal(OfferResult.Added, b.Offer(W("a", Level.Danger, 10f), 10f));
            Assert.Equal(OfferResult.Added, b.Offer(W("b", Level.Danger, 10.5f), 10.5f));
            Assert.Equal("b", b.Active[0].AbilityId);
            Assert.Equal("a", b.Active[1].AbilityId);
        }

        [Fact]
        public void TheSameAbilityReplacesItsOwnLine()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Replaced, b.Offer(W("a", Level.Danger, 10.4f), 10.4f));
            Assert.Equal(2, b.Active.Count);
            Assert.Equal("a", b.Active[0].AbilityId);
            Assert.Equal(10.4f, b.Active[0].Start);
        }

        [Fact]
        public void AThirdDangerEvictsTheOldest()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Evicted, b.Offer(W("c", Level.Danger, 10.4f), 10.4f));
            Assert.Equal("c", b.Active[0].AbilityId);
            Assert.Equal("b", b.Active[1].AbilityId);
        }

        [Fact]
        public void CautionNeverEvictsDanger()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Dropped, b.Offer(W("c", Level.Caution, 10.4f), 10.4f));
            Assert.Equal(2, b.Active.Count);
        }

        [Fact]
        public void DangerEvictsCautionFirst()
        {
            var b = new WarningBoard();
            b.Offer(W("caution", Level.Caution, 10f), 10f);
            b.Offer(W("old", Level.Danger, 10.2f), 10.2f);
            b.Offer(W("new", Level.Danger, 10.4f), 10.4f);
            Assert.Equal("new", b.Active[0].AbilityId);
            Assert.Equal("old", b.Active[1].AbilityId);
        }

        [Fact]
        public void FadingLinesMakeRoom()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 1f), 10f);     // ends 11.5, gone 11.8
            b.Offer(W("b", Level.Danger, 10.1f, 3f), 10.1f);
            Assert.Equal(OfferResult.Added, b.Offer(W("c", Level.Caution, 11.6f), 11.6f)); // a is fading
            Assert.Equal(2, b.Active.Count);
        }

        [Fact]
        public void LivesUntilTheHitPlusHalfASecondButAtLeast1_2Seconds()
        {
            Assert.Equal(12.84f, WarningBoard.EndAt(W("a", Level.Danger, 10f, 2.34f)), 3);
            Assert.Equal(11.2f, WarningBoard.EndAt(W("a", Level.Danger, 10f, 0.6f)), 3);
        }

        [Fact]
        public void ARealHitMovesTheEnd()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 2.34f), 10f);
            b.Hit("a", 1, 11.5f);
            Assert.True(b.Active[0].HitSeen);
            Assert.Equal(12f, WarningBoard.EndAt(b.Active[0]), 3);
            b.Hit("a", 1, 11.9f); // a second hit of the same attack doesn't move it again
            Assert.Equal(11.5f, b.Active[0].HitAt, 3);
            b.Hit("a", 2, 11.9f); // another boss's hit doesn't touch it
            Assert.Equal(11.5f, b.Active[0].HitAt, 3);
        }

        [Fact]
        public void FadesOutThenLeaves()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 2f), 10f); // ends 12.5
            Assert.Equal(1f, WarningBoard.Alpha(b.Active[0], 12.5f), 3);
            Assert.Equal(0.5f, WarningBoard.Alpha(b.Active[0], 12.65f), 3);
            b.Tick(12.79f);
            Assert.Single(b.Active);
            b.Tick(12.81f);
            Assert.Empty(b.Active);
        }

        [Fact]
        public void ProgressAndRemaining()
        {
            Warning w = W("a", Level.Danger, 10f, 2f);
            Assert.Equal(0f, WarningBoard.Progress(w, 10f), 3);
            Assert.Equal(0.5f, WarningBoard.Progress(w, 11f), 3);
            Assert.Equal(1f, WarningBoard.Progress(w, 13f), 3);
            Assert.Equal(1.5f, WarningBoard.Remaining(w, 10.5f), 3);
            Assert.Equal(0f, WarningBoard.Remaining(w, 13f), 3);
        }

        [Fact]
        public void SoundPlaysAtMostOncePerSecondPerAbility()
        {
            var b = new WarningBoard();
            Warning first = W("a", Level.Danger, 10f);
            b.Offer(first, 10f);
            Assert.True(first.PlaySound);
            Warning again = W("a", Level.Danger, 10.5f);
            b.Offer(again, 10.5f);
            Assert.False(again.PlaySound);
            Warning later = W("a", Level.Danger, 11f);
            b.Offer(later, 11f);
            Assert.True(later.PlaySound);
            Warning muted = W("b", Level.Danger, 12f, 2f, false);
            b.Offer(muted, 12f);
            Assert.False(muted.PlaySound);
        }

        [Fact]
        public void ClearEmptiesTheBoard()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Clear();
            Assert.Empty(b.Active);
        }
    }
}

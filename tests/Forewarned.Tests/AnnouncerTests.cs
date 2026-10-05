using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AnnouncerTests
    {
        [Fact]
        public void NewestAtTheBottomAndAtMostThree()
        {
            var a = new Announcer();
            a.Add("1", "one", 0f);
            a.Add("2", "two", 1f);
            a.Add("3", "three", 2f);
            a.Add("4", "four", 3f);
            Assert.Equal(3, a.Lines.Count);
            Assert.Equal("two", a.Lines[0].Text);
            Assert.Equal("four", a.Lines[2].Text);
        }

        [Fact]
        public void TheSameKeyRefreshesItsLine()
        {
            var a = new Announcer();
            a.Add("adds", "Charred Warriors incoming", 0f);
            a.Add("other", "x", 1f);
            a.Add("adds", "Charred Warriors incoming", 2f);
            Assert.Equal(2, a.Lines.Count);
            Assert.Equal("adds", a.Lines[1].Key);
            Assert.Equal(2f, a.Lines[1].Start);
        }

        [Fact]
        public void LastsFourSecondsThenFades()
        {
            var a = new Announcer();
            a.Add("k", "t", 10f);
            Assert.Equal(1f, Announcer.Alpha(a.Lines[0], 14f), 3);
            Assert.Equal(0.5f, Announcer.Alpha(a.Lines[0], 14.15f), 3);
            a.Tick(14.29f);
            Assert.Single(a.Lines);
            a.Tick(14.31f);
            Assert.Empty(a.Lines);
        }
    }
}

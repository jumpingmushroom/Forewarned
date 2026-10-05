using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TextCheckTests
    {
        [Theory]
        [InlineData("Get behind Fader", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        [InlineData("[enemy_fader]", false)]
        [InlineData("$enemy_fader", false)]
        public void IsClean(string text, bool expected)
        {
            Assert.Equal(expected, TextCheck.IsClean(text));
        }
    }
}

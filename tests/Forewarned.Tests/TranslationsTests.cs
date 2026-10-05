using System.Linq;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TranslationsTests
    {
        [Fact]
        public void ParsesKeysAndSkipsComments()
        {
            Translations t = Translations.Parse("# comment\nfader.spin.name = Spin\n\nbroken line\nempty =\n");
            Assert.Equal("Spin", t.Get("fader.spin.name"));
            Assert.Null(t.Get("empty"));
            Assert.Null(t.Get("missing"));
            Assert.Single(t.Keys);
        }

        [Fact]
        public void FallsBackToEnglish()
        {
            Translations english = Translations.Parse("a = A\nb = B");
            Translations german = Translations.Parse("a = Ä").WithFallback(english);
            Assert.Equal("Ä", german.Get("a"));
            Assert.Equal("B", german.Get("b"));
        }
    }
}

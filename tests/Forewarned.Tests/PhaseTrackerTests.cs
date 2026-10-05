using System.Collections.Generic;
using System.Linq;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class PhaseTrackerTests
    {
        private static readonly IReadOnlyList<Phase> Fader = new[]
        {
            new Phase { At = 0.85f, Key = "85" }, new Phase { At = 0.55f, Key = "55" },
            new Phase { At = 0.35f, Key = "35" }, new Phase { At = 0.25f, Key = "25" }
        };

        private static string Keys(IList<Phase> p) => string.Join(",", p.Select(x => x.Key));

        [Fact]
        public void AnnouncesEachThresholdOnceOnTheWayDown()
        {
            var t = new PhaseTracker();
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.9f)));
            Assert.Equal("85", Keys(t.Observe(1, Fader, 0.84f)));
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.86f))); // healed back over
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.84f))); // jitter: no repeat
            Assert.Equal("55,35", Keys(t.Observe(1, Fader, 0.3f)));
        }

        [Fact]
        public void JoiningMidFightStaysQuietAboutThePast()
        {
            var t = new PhaseTracker();
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.3f)));
            Assert.Equal("25", Keys(t.Observe(1, Fader, 0.2f)));
        }

        [Fact]
        public void ForgetStartsOver()
        {
            var t = new PhaseTracker();
            t.Observe(1, Fader, 0.9f);
            t.Observe(1, Fader, 0.5f);
            t.Forget(1);
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.9f)));
            Assert.Equal("85", Keys(t.Observe(1, Fader, 0.8f)));
        }
    }
}

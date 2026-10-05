using System.Linq;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class EikthyrTests
    {
        private static Engine Make() =>
            new Engine(new ModuleRegistry(BossList.All), DataFilesTests.English(), t => t == "$enemy_eikthyr" ? "Eikthyr" : t, new DefaultSettings());

        private static Scene At(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        private static TriggerEvent E(string trigger, float time) =>
            new TriggerEvent { BossId = 1, Prefab = "Eikthyr", Trigger = trigger, Time = time };

        [Fact]
        public void StompTellsYouToRunTenMetres()
        {
            Engine e = Make();
            e.OnTrigger(E("attack_stomp", 10f), At(0f, 8f), true);
            Warning w = Assert.Single(e.Board.Active);
            Assert.Equal("STOMP", w.Title);
            Assert.Equal("Run out, 10 m", w.Action);
            Assert.Equal(12.89f, w.HitAt, 3);
            Assert.Equal(Level.Danger, w.Level);
        }

        [Fact]
        public void ChargeOnlyWarnsThoseInFront()
        {
            Engine inFront = Make();
            inFront.OnTrigger(E("attack2", 10f), At(0f, 12f), true);
            Assert.Equal("Sidestep out of the line", Assert.Single(inFront.Board.Active).Action);

            Engine behind = Make();
            behind.OnTrigger(E("attack2", 10f), At(0f, -12f), true);
            Assert.Empty(behind.Board.Active);
            Assert.Equal("Eikthyr: Charge", Assert.Single(behind.Announcer.Lines).Text);
        }

        [Fact]
        public void AntlerIsCautionAndOffByDefault()
        {
            AbilitySpec antler = new EikthyrModule().Abilities.Single(a => a.Id == "eikthyr.antler");
            Assert.Equal(Level.Caution, antler.DefaultLevel);
            Assert.False(antler.DefaultOn);
            Engine e = Make();
            e.OnTrigger(E("attack1", 10f), At(0f, 2f), true);
            Assert.Empty(e.Board.Active);
        }
    }
}

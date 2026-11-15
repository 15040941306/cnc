using Rts.Core.Sim;
using Xunit;

namespace Rts.Tests
{
    public class WorldTests
    {
        [Fact]
        public void StateHash_MatchesIndependentFnv1aComputation()
        {
            // FNV-1a 64 over int32 LE (tick=100) followed by uint64 LE (random state=42).
            var world = new World(42);
            for (int i = 0; i < 100; i++)
                world.Step();
            Assert.Equal(100, world.Tick);
            Assert.Equal(0x3CEEBDDFD6E0C15BUL, world.StateHash());
        }

        [Fact]
        public void StateHash_ChangesWithTickAndRandomState()
        {
            var a = new World(1);
            var b = new World(1);
            Assert.Equal(a.StateHash(), b.StateHash());

            b.Step();
            Assert.NotEqual(a.StateHash(), b.StateHash());

            a.Step();
            Assert.Equal(a.StateHash(), b.StateHash());

            a.Random.NextUInt64();
            Assert.NotEqual(a.StateHash(), b.StateHash());
            Assert.NotEqual(new World(1).StateHash(), new World(2).StateHash());
        }
    }
}

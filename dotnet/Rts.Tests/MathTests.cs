using System;
using Rts.Core.Math;
using Xunit;

namespace Rts.Tests
{
    public class MathTests
    {
        [Fact]
        public void SimRandom_MatchesSplitMix64ReferenceVector()
        {
            var random = new SimRandom(0);
            Assert.Equal(0xE220A8397B1DCDAFUL, random.NextUInt64());
            Assert.Equal(0x6E789E6AA1B965F4UL, random.NextUInt64());
            Assert.Equal(0x06C45D188009454FUL, random.NextUInt64());
        }

        [Fact]
        public void SimRandom_ResumesFromSavedState()
        {
            var original = new SimRandom(12345);
            original.NextUInt64();
            var resumed = new SimRandom(original.State);
            for (int i = 0; i < 100; i++)
                Assert.Equal(original.NextUInt64(), resumed.NextUInt64());
        }

        [Fact]
        public void SimRandom_NextStaysInRange()
        {
            var random = new SimRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                Assert.InRange(random.Next(10), 0, 9);
                Assert.InRange(random.Next(-5, 5), -5, 4);
            }

            Assert.InRange(random.Next(int.MinValue, int.MaxValue), int.MinValue, int.MaxValue - 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(3, 3));
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(255, 255, 0, 0)]
        [InlineData(256, 511, 1, 1)]
        [InlineData(-1, -256, -1, -1)]
        [InlineData(-257, 0, -2, 0)]
        public void WorldPos_ToCell_FloorsTowardNegativeInfinity(int x, int y, int cellX, int cellY)
        {
            Assert.Equal(new CellPos(cellX, cellY), new WorldPos(x, y, 0).ToCell());
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 7)]
        [InlineData(-4, -1)]
        public void WorldPos_CenterOf_RoundTrips(int cellX, int cellY)
        {
            var cell = new CellPos(cellX, cellY);
            var center = WorldPos.CenterOf(cell, 12);
            Assert.Equal(cell, center.ToCell());
            Assert.Equal(cellX * 256 + 128, center.X);
            Assert.Equal(12, center.Z);
        }
    }
}

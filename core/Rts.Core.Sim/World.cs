using Rts.Core.Math;

namespace Rts.Core.Sim
{
    /// <summary>The single container of simulation state.</summary>
    public sealed class World
    {
        public int Tick { get; private set; }
        public SimRandom Random { get; }

        public World(ulong seed)
        {
            Random = new SimRandom(seed);
        }

        public void Step()
        {
            Tick++;
        }

        public ulong StateHash()
        {
            var hasher = new StateHasher();
            hasher.Add(Tick);
            hasher.Add(Random.State);
            return hasher.Value;
        }
    }
}

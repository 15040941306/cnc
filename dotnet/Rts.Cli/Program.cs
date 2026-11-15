using System;
using Rts.Core.Sim;

namespace Rts.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] != "run")
            {
                Console.Error.WriteLine("usage: Rts.Cli run [--ticks N] [--seed S]");
                return 2;
            }

            int ticks = 0;
            ulong seed = 0;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--ticks" && i + 1 < args.Length && int.TryParse(args[i + 1], out ticks))
                    i++;
                else if (args[i] == "--seed" && i + 1 < args.Length && ulong.TryParse(args[i + 1], out seed))
                    i++;
                else
                {
                    Console.Error.WriteLine($"bad argument: {args[i]}");
                    return 2;
                }
            }

            var world = new World(seed);
            for (int i = 0; i < ticks; i++)
                world.Step();

            Console.WriteLine($"tick={world.Tick} hash={world.StateHash():X16}");
            return 0;
        }
    }
}

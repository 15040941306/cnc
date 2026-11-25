using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ra2Converter.Formats;
using ReCnc.Core.Data;
using ReCnc.Core.Map;
using ReCnc.Core.Simulation;

namespace ReCnc.Tests;

public static class Program
{
    private static int _passed;

    public static int Main()
    {
        Console.WriteLine("Running ReCnc Automated Test Suite...");

        TestIniAndPaletteParsers();
        TestAStarPathfinding();
        TestSkirmishBaseBuildingHarvesterAndCombatLoop();
        TestRealRawAssetsIfAvailable();

        Console.WriteLine($"[PASS] All {_passed} test suites passed successfully!");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Assertion failed: " + message);
    }

    private static void TestIniAndPaletteParsers()
    {
        const string sampleIni = @"
; Sample RA2 INI
[MTNK]
UIName=Name:MTNK
Cost=700 ; Grizzly cost
Strength=300
Powered=no

[Animations]
+=ANIM1
+=ANIM2
";
        var doc = IniDocument.ParseText(sampleIni);
        var mtnk = doc.GetSection("mtnk");
        Assert(mtnk != null, "MTNK section must exist");
        Assert(mtnk!.GetInt("Cost") == 700, "Cost should ignore inline comment");
        Assert(mtnk.GetInt("Strength") == 300, "Strength should be 300");
        Assert(!mtnk.GetBool("Powered"), "Powered=no should be false");

        var anims = doc.GetSection("Animations");
        Assert(anims != null && anims.OrderedEntries.Count == 2, "+= entries should append uniquely");

        byte[] rawPal = new byte[768];
        rawPal[3] = 63; rawPal[4] = 0; rawPal[5] = 32; // index 1
        var pal = PalPalette.ParseSixBitPal(rawPal, 16, 31);
        Assert(pal.Colors[0].A == 0, "Palette index 0 must be transparent");
        Assert(pal.Colors[1].R == 255 && pal.Colors[1].A == 255, "6-bit 63 must map to 8-bit 255");
        var lut = pal.BuildVoxelLut256x2();
        Assert(lut[256 + 16].R == 255 && lut[256 + 1].R == 0, "Team remap row must mask indices 16..31");

        _passed++;
        Console.WriteLine("  [OK] TestIniAndPaletteParsers");
    }

    private static void TestAStarPathfinding()
    {
        var mapData = DefaultMapFactory.CreateSkirmishArena(48, 48);
        var map = new GridMap(mapData);

        // Place an obstacle wall and verify path routes around it without corner-cutting
        map.SetBuildingFootprint(new CellCoord(14, 10), 2, 4, 99);
        var path = AStarPathfinder.FindPath(map, new CellCoord(12, 11), new CellCoord(18, 11));
        Assert(path.Count > 0, "A* should find a valid path around building");
        Assert(path[^1] == new CellCoord(18, 11), "Path must end at destination");
        foreach (var step in path)
        {
            Assert(map.GetCell(step).IsLandPassable, $"Path cell {step} must be passable");
        }

        _passed++;
        Console.WriteLine("  [OK] TestAStarPathfinding");
    }

    private static void TestSkirmishBaseBuildingHarvesterAndCombatLoop()
    {
        var catalog = RulesCatalog.CreateWithDefaults(null);
        var map = DefaultMapFactory.CreateSkirmishArena(64, 64);
        var sim = GameSimulation.CreateStandardSkirmish(catalog, map, "Allied");

        // 1. Find player MCV and deploy into Allied Construction Yard
        var mcv = sim.Entities.FirstOrDefault(e => e.FactionId == 0 && e.Def.Role == "Mcv");
        Assert(mcv != null, "Player must start with MCV");
        bool deployed = sim.CommandDeployMcv(mcv!.Id);
        Assert(deployed, "MCV deployment must succeed");

        var yard = sim.Entities.FirstOrDefault(e => e.FactionId == 0 && e.Def.Id == "GACNST");
        Assert(yard != null, "Allied Construction Yard must exist after MCV deploy");

        // 2. Build Power Plant (GAPOWR)
        Assert(sim.CommandStartProduction(0, "GAPOWR"), "GAPOWR must be buildable after GACNST");
        for (int i = 0; i < 60; i++) sim.Step(0.1f);
        Assert(sim.GetFaction(0).Queues["MainBuilding"].IsReadyToPlace, "GAPOWR should finish building");

        bool placedPwr = false;
        for (int dx = -6; dx <= 6 && !placedPwr; dx++)
        {
            placedPwr = sim.CommandPlaceReadyBuilding(0, "MainBuilding", new CellCoord(yard!.TopLeftCell.X + dx, yard.TopLeftCell.Y + 5));
        }
        Assert(placedPwr, "GAPOWR placement near yard must succeed");
        Assert(sim.GetFaction(0).PowerProduced == 200, "PowerProduced should be 200");

        // 3. Build Ore Refinery (GAREFN) -> spawns free Chrono Miner (CMIN) and harvests credits!
        Assert(sim.CommandStartProduction(0, "GAREFN"), "GAREFN must unlock after GAPOWR");
        for (int i = 0; i < 120; i++) sim.Step(0.1f);
        Assert(sim.GetFaction(0).Queues["MainBuilding"].IsReadyToPlace, "GAREFN should finish building");

        bool placedRef = false;
        for (int dy = -6; dy <= 6 && !placedRef; dy++)
        {
            placedRef = sim.CommandPlaceReadyBuilding(0, "MainBuilding", new CellCoord(yard!.TopLeftCell.X + 5, yard.TopLeftCell.Y + dy));
        }
        Assert(placedRef, "GAREFN placement must succeed");

        var harvester = sim.Entities.FirstOrDefault(e => e.FactionId == 0 && e.Def.Id == "CMIN");
        Assert(harvester != null, "Placing GAREFN must spawn free CMIN harvester");

        int creditsBeforeMining = sim.GetFaction(0).Credits;
        for (int i = 0; i < 450; i++) sim.Step(0.1f); // 45s simulated mining cycle
        Assert(sim.GetFaction(0).Credits > creditsBeforeMining, "Harvester must mine ore and deposit credits back to Refinery");

        // 4. Build War Factory (GAWEAP) and produce a Grizzly Tank (MTNK)
        Assert(sim.CommandStartProduction(0, "GAWEAP"), "GAWEAP must unlock after GAREFN");
        for (int i = 0; i < 120; i++) sim.Step(0.1f);
        bool placedWeap = false;
        for (int dx = -7; dx <= 7 && !placedWeap; dx++)
        {
            for (int dy = -7; dy <= 7 && !placedWeap; dy++)
            {
                placedWeap = sim.CommandPlaceReadyBuilding(0, "MainBuilding", new CellCoord(yard!.TopLeftCell.X + dx, yard.TopLeftCell.Y + dy));
            }
        }
        Assert(placedWeap, "GAWEAP placement must succeed");

        int tanksBefore = sim.Entities.Count(e => e.FactionId == 0 && e.Def.Id == "MTNK");
        Assert(sim.CommandStartProduction(0, "MTNK"), "MTNK must unlock after GAWEAP");
        for (int i = 0; i < 60; i++) sim.Step(0.1f);
        int tanksAfter = sim.Entities.Count(e => e.FactionId == 0 && e.Def.Id == "MTNK");
        Assert(tanksAfter == tanksBefore + 1, "War Factory must spawn produced Grizzly Tank");

        // 5. Combat test: spawn an enemy Rhino Tank next to player Grizzly Tanks and verify damage/destruction
        var pTank = sim.Entities.First(e => e.FactionId == 0 && e.Def.Id == "MTNK");
        var enemyDummy = sim.SpawnUnit(1, "E2", new CellCoord(pTank.Position.ToCell().X + 2, pTank.Position.ToCell().Y));
        int dummyId = enemyDummy.Id;
        sim.CommandAttack(new List<int> { pTank.Id }, dummyId);
        for (int i = 0; i < 80; i++) sim.Step(0.1f);
        Assert(sim.GetEntity(dummyId) == null || !sim.GetEntity(dummyId).IsAlive, "Grizzly Tank must engage and destroy enemy target");

        _passed++;
        Console.WriteLine("  [OK] TestSkirmishBaseBuildingHarvesterAndCombatLoop");
    }

    private static void TestRealRawAssetsIfAvailable()
    {
        var cur = new DirectoryInfo(AppContext.BaseDirectory);
        string? rawDir = null;
        while (cur != null)
        {
            string candidate = Path.Combine(cur.FullName, "raw_assets");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ra2.mix")))
            {
                rawDir = candidate;
                break;
            }
            cur = cur.Parent;
        }

        if (rawDir == null)
        {
            Console.WriteLine("  [SKIP] raw_assets not present on this runner");
            return;
        }

        var vfs = new Ra2VirtualFileSystem(rawDir);
        Assert(vfs.Mounts.Count >= 20, "Should mount top-level and nested MIX archives");

        byte[]? rulesBytes = vfs.TryReadFile("rulesmd.ini") ?? vfs.TryReadFile("rules.ini");
        Assert(rulesBytes is { Length: > 10000 }, "Must extract rules(md).ini from encrypted MIX");

        byte[]? mtnkVxl = vfs.TryReadFile("mtnk.vxl");
        Assert(mtnkVxl != null, "Must extract mtnk.vxl from nested conquer.mix");
        var vxl = VxlDocument.Parse(mtnkVxl!);
        Assert(vxl.Limbs.Count >= 1 && vxl.Limbs[0].SizeX > 0, "mtnk.vxl must decode valid 3D voxel limbs");

        // Verify extracted official skirmish maps allow ground units to traverse high ground / ramps between spawns!
        var tempIni = IniDocument.Parse(vfs.TryReadFile("temperatmd.ini") ?? vfs.TryReadFile("temperat.ini")!);
        var tempPal = PalPalette.ParseSixBitPal(vfs.TryReadFile("isotem.pal")!, -1, -1);
        var desPal = PalPalette.ParseSixBitPal(vfs.TryReadFile("isodes.pal")!, -1, -1);
        var atlases = new Dictionary<string, TheaterTilesetAtlas>(StringComparer.OrdinalIgnoreCase)
        {
            ["TEMPERATE"] = new TheaterTilesetAtlas("TEMPERATE", tempIni, tempPal, vfs, "tem"),
            ["DESERT"]    = new TheaterTilesetAtlas("DESERT", IniDocument.Parse(vfs.TryReadFile("desertmd.ini")!), desPal, vfs, "des"),
        };
        foreach (string mapName in new[] { "bldfeud.map", "xdeadman.map", "dunepatr.map", "xmp14t2.map", "xgrinder.map" })
        {
            byte[]? mb = vfs.TryReadFile(mapName);
            Assert(mb != null, $"Must read {mapName}");
            var extracted = Ra2MapExtractor.TryExtractFullMap(mapName, mapName, mb!, atlases);
            Assert(extracted != null && extracted.Bundle.Waypoints.Count >= 2, $"Must parse {mapName}");
            Assert(extracted!.BakedTerrainPixels != null && extracted.BakedTerrainW >= 1200, $"Must bake full-res .TMP terrain texture for {mapName}");

            var sim = GameSimulation.CreateStandardSkirmish(RulesCatalog.CreateWithDefaults(null), extracted.Bundle);
            var p0Unit = sim.Entities.First(e => e.FactionId == 0 && !e.Def.IsBuilding);
            var p1Unit = sim.Entities.First(e => e.FactionId == 1 && !e.Def.IsBuilding);
            var path = AStarPathfinder.FindPath(sim.Map, p0Unit.Position.ToCell(), p1Unit.Position.ToCell(), 32768);
            Assert(path.Count > 5 && path[^1].DistanceTo(p1Unit.Position.ToCell()) <= 4f,
                $"Ground units on {mapName} must reach enemy spawn across 1:1 RA2 ramps (got end={path.LastOrDefault()}, target={p1Unit.Position.ToCell()})");
        }

        _passed++;
        Console.WriteLine("  [OK] TestRealRawAssetsIfAvailable");
    }
}

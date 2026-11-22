using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ra2Converter.Formats;

public sealed class Ra2VirtualFileSystem
{
    private static readonly string[] KnownNestedMixNames =
    {
        "localmd.mix", "conqmd.mix", "genermd.mix", "cameomd.mix", "audiomd.mix",
        "isogenmd.mix", "isotemmd.mix", "isosnomd.mix", "isourbmd.mix", "isoubnmd.mix", "isolunmd.mix", "isodesmd.mix",
        "cachemd.mix", "loadmd.mix", "sidec01.mix", "sidec02.mix", "sidec02md.mix",
        "local.mix", "conquer.mix", "generic.mix", "cameo.mix", "audio.mix",
        "isogen.mix", "isotemp.mix", "isosnow.mix", "isourb.mix", "neutral.mix", "cache.mix", "load.mix",
        "temperat.mix", "snow.mix", "urban.mix", "urbanmd.mix", "lunarmd.mix", "desertmd.mix"
    };

    private readonly string _rawRoot;
    private readonly List<MixArchive> _mounts = new();

    public IReadOnlyList<MixArchive> Mounts => _mounts;

    public Ra2VirtualFileSystem(string rawAssetsDir)
    {
        _rawRoot = rawAssetsDir;
        MountAll();
    }

    private void MountAll()
    {
        if (!Directory.Exists(_rawRoot))
            throw new DirectoryNotFoundException($"Raw assets directory not found: {_rawRoot}");

        var allMixFiles = Directory.GetFiles(_rawRoot, "*.mix", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_rawRoot, "*.MIX", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Priority order matching RA2/YR engine:
        // 1. expandmd*.mix / expand*.mix (patches override base)
        // 2. ra2md.mix, langmd.mix, multimd.mix, mapsmd*.mix
        // 3. ra2.mix, language.mix, multi.mix, maps*.mix, others
        var orderedTopPaths = allMixFiles
            .OrderBy(p => GetPriorityTier(Path.GetFileName(p)))
            .ThenByDescending(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topArchives = new List<MixArchive>();
        foreach (string path in orderedTopPaths)
        {
            try
            {
                var archive = MixArchive.Open(path);
                _mounts.Add(archive);
                topArchives.Add(archive);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Skipping unreadable MIX {Path.GetFileName(path)}: {ex.Message}");
            }
        }

        // Mount nested sub-MIX archives inside top-level MIXes (e.g. localmd.mix, conqmd.mix, cameo.mix, audio.mix)
        foreach (var parent in topArchives)
        {
            foreach (string nestedName in KnownNestedMixNames)
            {
                parent.RegisterCandidateName(nestedName);
                byte[]? nestedBytes = parent.TryReadBytes(nestedName);
                if (nestedBytes == null || nestedBytes.Length < 16) continue;
                try
                {
                    var child = MixArchive.Parse($"{parent.Name}/{nestedName}", nestedBytes);
                    _mounts.Add(child);
                }
                catch
                {
                    // Ignore non-MIX collisions
                }
            }
        }
    }

    private static int GetPriorityTier(string fileName)
    {
        string lower = fileName.ToLowerInvariant();
        if (lower.StartsWith("expandmd")) return 0;
        if (lower.StartsWith("expand")) return 1;
        if (lower.StartsWith("ecachemd") || lower.StartsWith("elocalmd")) return 2;
        if (lower == "ra2md.mix" || lower == "langmd.mix") return 3;
        if (lower.Contains("md")) return 4;
        if (lower == "ra2.mix" || lower == "language.mix") return 5;
        return 6;
    }

    public byte[]? TryReadFile(string filename)
    {
        // 1. Check loose override files first
        string loosePath = Path.Combine(_rawRoot, filename);
        if (File.Exists(loosePath))
        {
            return File.ReadAllBytes(loosePath);
        }

        // 2. Search mounted archives in priority order
        foreach (var archive in _mounts)
        {
            archive.RegisterCandidateName(filename);
            byte[]? bytes = archive.TryReadBytes(filename);
            if (bytes != null && bytes.Length > 0)
            {
                return bytes;
            }
        }
        return null;
    }

    public bool Exists(string filename)
    {
        if (File.Exists(Path.Combine(_rawRoot, filename))) return true;
        foreach (var archive in _mounts)
        {
            if (archive.Contains(filename)) return true;
        }
        return false;
    }
}

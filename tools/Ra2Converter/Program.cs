using System;
using System.IO;

namespace Ra2Converter;

public static class Program
{
    public static int Main(string[] args)
    {
        string repoRoot = FindRepoRoot();
        string rawDir = Path.Combine(repoRoot, "raw_assets");
        string unityDir = Path.Combine(repoRoot, "unity", "re_cnc");

        string? singleMapPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--raw" || args[i] == "-r") && i + 1 < args.Length)
                rawDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--unity" || args[i] == "-u") && i + 1 < args.Length)
                unityDir = Path.GetFullPath(args[++i]);
            else if ((args[i] == "--map" || args[i] == "-m") && i + 1 < args.Length)
                singleMapPath = Path.GetFullPath(args[++i]);
        }

        Console.WriteLine("============================================================");
        Console.WriteLine("  ReCnc - Red Alert 2 / Yuri's Revenge Offline 3D Converter ");
        Console.WriteLine("============================================================");
        Console.WriteLine($"原始素材目录 : {rawDir}");
        Console.WriteLine($"Unity 工程   : {unityDir}");

        if (!Directory.Exists(rawDir))
        {
            Console.Error.WriteLine($"[错误] 未找到 raw_assets 目录: {rawDir}");
            return 1;
        }

        var pipeline = new AssetConversionPipeline(rawDir, unityDir);
        if (!string.IsNullOrEmpty(singleMapPath))
        {
            if (!File.Exists(singleMapPath))
            {
                Console.Error.WriteLine($"[错误] 指定的地图文件不存在: {singleMapPath}");
                return 1;
            }
            var entry = pipeline.ConvertSingleExternalMap(singleMapPath);
            if (entry == null)
            {
                Console.Error.WriteLine($"[错误] 无法解析地图文件: {singleMapPath}");
                return 1;
            }
            Console.WriteLine($"[地图导入成功] {entry.DisplayName} ({entry.Theater}, {entry.PlayableWidth}x{entry.PlayableHeight}) -> {entry.JsonRelPath}");
            return 0;
        }

        var manifest = pipeline.RunAll();

        Console.WriteLine("------------------------------------------------------------");
        Console.WriteLine($"[全部完成] 3D 模型: {manifest.Models.Count}, 图标: {manifest.Cameos.Count}, 立绘: {manifest.Sprites.Count}, 音频: {manifest.Audios.Count}, 地图: {manifest.Maps.Count}");
        return 0;
    }

    private static string FindRepoRoot()
    {
        var cur = new DirectoryInfo(AppContext.BaseDirectory);
        while (cur != null)
        {
            if (File.Exists(Path.Combine(cur.FullName, "ReCnc.slnx")) || Directory.Exists(Path.Combine(cur.FullName, "raw_assets")))
                return cur.FullName;
            cur = cur.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}

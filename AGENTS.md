# AGENTS.md

## 项目概述
《红色警戒2 / 尤里的复仇》Unity 全 3D 重制单机原型工程，采用三层解耦架构：
- `tools/collect_ra2_assets.py`：从 Windows 正版安装目录收集 `.mix` 与外置资源至 `raw_assets/`。
- `tools/Ra2Converter/`：离线资源解包与 3D 转换 CLI 工具（解密嵌套 `.mix`，解析 `INI`/`CSF`/`PAL`，将 `.vxl+.hva` 体素转为多部件 3D `.obj` 网格，将 `.shp` 转为 `.png`，将 `.bag+.idx` IMA ADPCM 音频转为 `.wav`）。
- `tools/blender_remaster/`：基于 Blender 5.x 无界面 Python (`bpy`) 的 3D 硬表面精模批量生成器，参照原版立绘与比例导出全部 28 套分部件 `.fbx` PBR 精模（含车体/炮塔/炮管/雷达旋转层级）并渲染等距预览图至 `artifacts/blender_previews/`。
- `packages/com.recnc.core/`：纯 C# 核心 RTS 模拟层（`netstandard2.1` + C# 9.0，零 Unity 依赖），实现地图网格、A* 寻路、MCV 展开、基地建造、电力与科技树、矿车自动采矿经济、炮塔独立瞄准战斗与遭遇战 AI。
- `packages/com.recnc.unity/`：Unity 2022.3 LTS (3D URP) 表现与交互本地包，包含阵营染色 Shader、3D 地形生成、3D RTS 相机、框选/移动/攻击指令、右侧建造栏 UI 及一键场景装配菜单。
- `unity/re_cnc/`：Unity 2022.3.62f3 主工程。

## 常用构建与验证命令
```bash
# 编译整个 .NET 解决方案（产物统一输出到 artifacts/，不污染 Unity 包）
dotnet build ReCnc.slnx

# 运行自动化测试套件（包含解析器、A* 寻路、RTS 建造采矿战斗闭环以及真实 raw_assets 验证）
dotnet run --project tests/ReCnc.Tests/ReCnc.Tests.csproj

# 将 raw_assets/ 中的原版红警2资源离线转换输出到 Unity 工程目录
dotnet run --project tools/Ra2Converter/Ra2Converter.csproj

# 使用 Blender 无界面批量生成全套 28 个翻新 3D .fbx 模型与等距渲染预览图
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python tools/blender_remaster/build_all_models.py
```

## 注意事项
- `raw_assets/`、`unity/**/Assets/_ReCncGenerated*` 与 `unity/**/Assets/StreamingAssets/Ra2Converted*` 包含从原版提取/转换的受版权保护资产，已在 `.gitignore` 中排除，切勿强制提交到 Git。
- `packages/com.recnc.core` 与 `packages/com.recnc.unity` 需保持兼容 Unity 2022.3 LTS（`netstandard2.1` / C# 9.0 语法）。

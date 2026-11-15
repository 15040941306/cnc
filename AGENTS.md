# AGENTS.md

给在本仓库工作的大模型和自动化代理的规则。设计依据见 [docs/architecture.md](docs/architecture.md)。

## 项目是什么

受经典等距 RTS 启发的单机游戏：纯 C# 模拟核心（`core/`）+ Unity 表现层（`unity/`）。只做单机，不做联机。

## 常用命令

```bash
dotnet test dotnet/Rts.slnx                                        # 编译并运行全部单元测试
python3 tools/ci/check_repo.py                                     # 禁止文件检查 + core 依赖规则检查
dotnet run --project dotnet/Rts.Cli -- run --ticks 1000 --seed 1   # 无画面运行，输出状态哈希
pre-commit install                                                 # 克隆后执行一次
```

提交前三条都必须通过。状态哈希发生变化时，在提交说明里解释原因。

## 目录

- `core/<程序集名>/`：纯 C# 源码和 asmdef，同时是 Unity 本地包（`core/package.json`）。
- `dotnet/<程序集名>/`：与 `core/` 下同名目录一一对应的 csproj，只用于脱离 Unity 编译和测试，不放源码。
- `dotnet/Rts.Tests/`：xUnit 测试。`dotnet/Rts.Cli/`：命令行工具。
- `docs/specs/`：规格文档。`docs/adr/`：架构决策记录。
- `content/`：规则数据。`assets/`：原创素材（CC BY 4.0）。`tools/`：脚本。
- `Local/`：已忽略，放本机配置和从原版导出的任何内容。

新增 core 程序集时，要同时添加 `core/X/X.asmdef`（`noEngineReferences: true`）和 `dotnet/X/X.csproj`，并让两边的引用关系一致。

## 规则

1. **资料来源**：只使用本仓库的 `docs/` 和代码。不要搜索、阅读或引用 OpenRA 及其模组、EA 公开的 C&C 源码、XCC 等工具的源码，也不要凭记忆复刻其他 RTS 开源项目的代码结构。
2. **规格先行**：新增组件、系统或文件格式前，先在 `docs/specs/` 写规格（模板见 `docs/specs/README.md`），由人审核后再写代码。
3. **一次只改一个模块**，每次提交不超过约 500 行，并附上测试。
4. **`core/` 的代码必须能被 Unity 6.3 编译**：C# 9、.NET Standard 2.1，不使用 nullable 注解、file-scoped namespace、`record struct` 等更新的语法。
5. **`core/` 禁止**：`UnityEngine` / `UnityEditor`；`System.Random`（用 `SimRandom`）；`DateTime.Now`、`Stopwatch` 等真实时间；遍历 `Dictionary` 或 `HashSet`；静态可变状态；多线程修改模拟状态。
6. **模拟状态只用整数**。中间计算可以用浮点数，写回状态前取整。
7. Unity 层不直接修改模拟状态，只提交命令；模拟层只输出状态和事件。
8. 数值常量放进规则数据或 `SimConstants`，不在代码中硬编码。
9. 命名：类型用 PascalCase，私有字段用 `_camelCase`；一个文件一个公开类型；命名空间和目录一致。
10. **依赖**：只允许 MIT、BSD、Apache-2.0、zlib 这类宽松许可证，禁止 GPL/LGPL/AGPL。新增依赖要登记到 `THIRD_PARTY.md`。
11. **原版内容**：原版游戏文件、原版 ini 的副本、从原版导出的任何内容都不得进入仓库，也不得作为图像生成、LoRA 训练或 ControlNet 的输入。

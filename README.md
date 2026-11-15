# cnc

受经典等距 RTS 启发的单机即时战略游戏：采矿、建造、战斗、电脑对手、战役。代码和素材全部重新制作。

本项目与 Electronic Arts 无关，仓库中不包含任何原版游戏文件。

## 当前状态

M0：仓库骨架。目前只有模拟核心的基础类型（坐标、随机数、状态哈希）和测试，还不能玩。路线图见 [docs/architecture.md](docs/architecture.md) 第 13 节。

## 结构

| 目录 | 内容 |
|---|---|
| `core/` | 纯 C# 模拟核心，不依赖 Unity，同时作为 Unity 本地包 |
| `dotnet/` | 让 `core/` 脱离 Unity 编译和测试的工程、单元测试、命令行工具 |
| `unity/` | Unity 工程（见 [unity/README.md](unity/README.md)） |
| `docs/` | 架构文档、规格文档、架构决策记录 |
| `content/` | 规则数据 |
| `assets/` | 原创素材 |
| `tools/` | CI 检查脚本、素材流水线 |

## 开发

需要 [.NET SDK 10](https://dotnet.microsoft.com/download)、Python 3、[pre-commit](https://pre-commit.com/)。

```bash
pre-commit install
dotnet test dotnet/Rts.slnx
python3 tools/ci/check_repo.py
dotnet run --project dotnet/Rts.Cli -- run --ticks 1000 --seed 1
```

编码规则见 [AGENTS.md](AGENTS.md)。

## 原版游戏文件

开发期的对照功能（M1 起）需要自备合法的原版游戏，路径通过环境变量 `RTS_ORIGINAL_DIR` 提供。原版文件和从中导出的任何内容只能放在已忽略的 `Local/` 目录，不得提交。

## 许可证

- 代码：[MIT](LICENSE)
- `assets/` 下的美术、音频和 `content/` 下的地图：[CC BY 4.0](assets/LICENSE)
- 第三方依赖：见 [THIRD_PARTY.md](THIRD_PARTY.md)

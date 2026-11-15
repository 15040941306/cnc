# Unity 工程

这个目录还没有 Unity 工程：工程文件需要由 Unity 编辑器生成，M0 的骨架是在没有 Unity 的环境里建立的。

## 第一次创建

1. 安装 Unity Hub 和 Unity 2022 LTS（2022.3.x）。在中国大陆和港澳，从 [unity.cn](https://unity.cn/releases) 下载中国版 Hub 和编辑器；Unity 6 及之后的版本在这些地区不提供。
2. 先把本文件移到别处（Unity Hub 通常要求目标目录为空），在 Hub 里用 **2D (URP)** 模板新建工程（模板名称以 Hub 里显示的为准），位置选仓库根目录，工程名填 `unity`。创建后把本文件放回来。
3. 在 `unity/Packages/manifest.json` 的 `dependencies` 里加一行：
   ```json
   "com.rts.core": "file:../../core",
   ```
4. 回到编辑器，确认 Package Manager 里出现 **Rts Core**，Console 没有编译错误。
5. 提交 `unity/Assets`、`unity/Packages`、`unity/ProjectSettings`，以及 Unity 在 `core/` 下生成的 `.meta` 文件。`ProjectSettings/ProjectVersion.txt` 会把 Unity 的具体小版本固定下来。

## 约定

- 这里只放渲染、UI、输入、音频和编辑器工具，代码放在 `Assets/Rts.Unity.*/`。
- 不直接修改模拟状态，只向模拟层提交命令。
- 场景和界面尽量用代码生成，少做手工拖拽配置。

# 基础数学类型（Rts.Core.Math）与状态哈希

## 坐标

- `CellPos(int X, int Y)`：地图格坐标。
- `WorldPos(int X, int Y, int Z)`：世界坐标，每格 `WorldPos.UnitsPerCell = 256` 个单位。
- `WorldPos.ToCell()`：X、Y 各自除以 256 并**向负无穷取整**。例：`(-1, -256)` → 格 `(-1, -1)`，`(-257, 0)` → 格 `(-2, 0)`。
- `WorldPos.CenterOf(cell, z)`：`(cell.X * 256 + 128, cell.Y * 256 + 128, z)`。

## 朝向

**待定**：精度取 256 还是 1024 还没有决定（architecture.md §15）。定下来之前不实现 `Facing`。

## SimRandom

模拟层唯一允许的随机数来源。

- 算法：SplitMix64。全部状态是一个 `ulong`（`State`）。用 `new SimRandom(state)` 可以从保存的状态继续同一序列。
- `NextUInt64()`：
  ```
  State += 0x9E3779B97F4A7C15
  z = State
  z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9
  z = (z ^ (z >> 27)) * 0x94D049BB133111EB
  return z ^ (z >> 31)
  ```
  全部为 64 位无符号运算，溢出回绕。
- `Next(maxExclusive)`：取 `NextUInt64()` 的高 32 位 `r`，返回 `(r * maxExclusive) >> 32`，范围 `[0, maxExclusive)`。`maxExclusive <= 0` 时抛 `ArgumentOutOfRangeException`。
- `Next(min, maxExclusive)`：`span = maxExclusive - min`（64 位），返回 `min + ((r * span) >> 32)`。`maxExclusive <= min` 时抛异常。
- 这种取范围的方式有极小的偏差（小于 `span / 2^32`），对游戏逻辑可以接受。

测试向量：种子 0 的前三个输出是 `0xE220A8397B1DCDAF`、`0x6E789E6AA1B965F4`、`0x06C45D188009454F`。

## 状态哈希

`World.StateHash()` 用于回放回归测试：同样的初始参数和命令日志必须得到同样的哈希。

- 算法：FNV-1a 64 位（offset basis `0xCBF29CE484222325`，prime `0x100000001B3`）。
- 每个字段按**小端字节序**逐字节喂入，与运行平台的字节序无关。
- 字段顺序（新增状态时追加到末尾，并更新这里）：
  1. `Tick`（int32）
  2. `Random.State`（uint64）

测试向量：`new World(42)` 执行 100 次 `Step()` 后哈希为 `0x3CEEBDDFD6E0C15B`。

## tick

`SimConstants.TicksPerSecond = 15`（最终值待定，architecture.md §15）。

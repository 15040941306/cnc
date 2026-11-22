using System;
using System.Collections.Generic;

namespace ReCnc.Core.Map
{
    public static class AStarPathfinder
    {
        private static readonly int[] NeighborDx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] NeighborDy = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly float[] NeighborCost = { 1f, 1.4142f, 1f, 1.4142f, 1f, 1.4142f, 1f, 1.4142f };

        public static List<CellCoord> FindPath(GridMap map, CellCoord start, CellCoord goal, int maxExpanded = 4096)
        {
            var result = new List<CellCoord>();
            if (!map.InBounds(start)) return result;

            CellCoord resolvedGoal = map.InBounds(goal) ? map.FindNearestPassable(goal, 6) : start;
            if (start == resolvedGoal)
            {
                return result;
            }

            int width = map.Width;
            int total = width * map.Height;
            var gScore = new float[total];
            var cameFrom = new int[total];
            var closed = new bool[total];
            for (int i = 0; i < total; i++)
            {
                gScore[i] = float.PositiveInfinity;
                cameFrom[i] = -1;
            }

            int startIdx = start.Y * width + start.X;
            int goalIdx = resolvedGoal.Y * width + resolvedGoal.X;
            gScore[startIdx] = 0f;

            var open = new List<int>(128) { startIdx };
            int bestFallbackIdx = startIdx;
            float bestFallbackHeuristic = Heuristic(start, resolvedGoal);
            int expanded = 0;

            while (open.Count > 0 && expanded < maxExpanded)
            {
                int bestListPos = 0;
                int currentIdx = open[0];
                float bestF = gScore[currentIdx] + HeuristicIdx(currentIdx, width, resolvedGoal);
                for (int i = 1; i < open.Count; i++)
                {
                    int candidateIdx = open[i];
                    float f = gScore[candidateIdx] + HeuristicIdx(candidateIdx, width, resolvedGoal);
                    if (f < bestF)
                    {
                        bestF = f;
                        currentIdx = candidateIdx;
                        bestListPos = i;
                    }
                }

                open[bestListPos] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);

                if (closed[currentIdx]) continue;
                closed[currentIdx] = true;
                expanded++;

                if (currentIdx == goalIdx)
                {
                    Reconstruct(cameFrom, startIdx, goalIdx, width, result);
                    return result;
                }

                int cx = currentIdx % width;
                int cy = currentIdx / width;
                float hRem = Heuristic(new CellCoord(cx, cy), resolvedGoal);
                if (hRem < bestFallbackHeuristic)
                {
                    bestFallbackHeuristic = hRem;
                    bestFallbackIdx = currentIdx;
                }

                for (int dir = 0; dir < 8; dir++)
                {
                    int nx = cx + NeighborDx[dir];
                    int ny = cy + NeighborDy[dir];
                    if (!map.CanStep(cx, cy, nx, ny)) continue;

                    int nIdx = ny * width + nx;
                    if (closed[nIdx]) continue;

                    var cell = map.GetCell(nx, ny);
                    float terrainMul = cell.Terrain == TerrainKind.Road ? 0.85f : 1.0f;
                    float tentativeG = gScore[currentIdx] + NeighborCost[dir] * terrainMul;
                    if (tentativeG < gScore[nIdx])
                    {
                        gScore[nIdx] = tentativeG;
                        cameFrom[nIdx] = currentIdx;
                        open.Add(nIdx);
                    }
                }
            }

            if (bestFallbackIdx != startIdx)
            {
                Reconstruct(cameFrom, startIdx, bestFallbackIdx, width, result);
            }
            return result;
        }

        private static float HeuristicIdx(int idx, int width, CellCoord goal)
        {
            return Heuristic(new CellCoord(idx % width, idx / width), goal);
        }

        private static float Heuristic(CellCoord a, CellCoord b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            int diag = Math.Min(dx, dy);
            int straight = Math.Max(dx, dy) - diag;
            return diag * 1.4142f + straight;
        }

        private static void Reconstruct(int[] cameFrom, int startIdx, int endIdx, int width, List<CellCoord> path)
        {
            int cur = endIdx;
            while (cur != -1 && cur != startIdx)
            {
                path.Add(new CellCoord(cur % width, cur / width));
                cur = cameFrom[cur];
            }
            path.Reverse();
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;

namespace DLA
{
    static class Utils
    {
        private static readonly object rndLock = new object();
        private static readonly System.Random globalRnd = new System.Random();
        public static List<Vector2Int> jitterOffsets = new List<Vector2Int>();
        public static readonly Vector2Int SENTINEL = new Vector2Int(int.MinValue, int.MinValue);
        public static float[,] GaussianBlur(float[,] toBlur, int radius, float standardDeviation)
        {
            int width = toBlur.GetLength(0);
            int height = toBlur.GetLength(1);
            float[] kernel = CalculateKernel(radius, standardDeviation);

            float[,] temp = new float[width, height];
            //horizontal pass
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;

                    for (int i = -radius; i <= radius; i++)
                    {
                        int sampleX = Mathf.Clamp(x + i, 0, width - 1);
                        sum += toBlur[sampleX, y] * kernel[i + radius];
                    }
                    temp[x, y] = sum;
                }
            }
            //vertical pass
            float[,] blurredResult = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;

                    for (int i = -radius; i <= radius; i++)
                    {
                        int sampleY = Mathf.Clamp(y + i, 0, height - 1);
                        sum += temp[x, sampleY] * kernel[i + radius];
                    }
                    blurredResult[x, y] = sum;
                    if (blurredResult[x, y] == 0f) continue;
                    //  Debug.Log($"blurring ({toBlur[x, y]}) into ({blurredResult[x,y]}) ({sum})");
                }
            }
            return blurredResult;

        }
        public static float[] CalculateKernel(int radius, float standardDeviation)
        {
            int kernelSize = 2 * radius + 1;
            float[] kernel = new float[kernelSize];
            float twoDeviationSquared = 2 * standardDeviation * standardDeviation;
            float inverseDeviationRoot = 1 / Mathf.Sqrt(twoDeviationSquared * Mathf.PI);
            float total = 0f;

            for (int i = -radius; i <= radius; i++)
            {
                float dist = i * i;
                int idx = i + radius;
                kernel[idx] = inverseDeviationRoot * Mathf.Exp(-dist / twoDeviationSquared);
                total += kernel[idx];
            }
            for (int i = 0; i < kernelSize; i++)
            {
                kernel[i] /= total;
                // Debug.Log($"kernel at {i} = {kernel[i]}");
            }
            return kernel;
        }
        public static float[,] AutoExpose(float[,] map)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);

            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float point = map[x, y];

                    if (point < minH) {
                        minH = point;
                    }
                    if (point > maxH) {
                        maxH = point;
                    }
                }
            }

            float range = maxH - minH;
            if (range <= 0f) return map;


            float[,] norm = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    norm[x, y] = (map[x, y] - minH) / range;
                }
            }

            return norm;
        }
        public static float[,] AddMultidimensionalFloats(float[,] a, float[,] b, float multiplicationFactorOfB = 1)
        {
            int width = a.GetLength(0);
            int height = a.GetLength(1);
            float[,] combined = new float[width, height];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    combined[i, j] = a[i, j] + b[i, j] * multiplicationFactorOfB;
                }
            }
            return combined;
        }
        public static float[,] MultiplyMultidimensionalFloats(float[,] a, float b)
        {
            int width = a.GetLength(0);
            int height = a.GetLength(1);
            float[,] combined = new float[width, height];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    combined[i, j] = a[i, j] * b;
                }
            }
            return combined;
        }
        public static int[,] CalculateWeights(bool[,] DLAmap, Dictionary<Vector2Int, Vector2Int> parentMap)
        {
            int res = DLAmap.GetLength(0);

            Dictionary<Vector2Int, List<Vector2Int>> children = new Dictionary<Vector2Int, List<Vector2Int>>();
            Dictionary<Vector2Int, int> inDegree = new Dictionary<Vector2Int, int>();

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    if (!DLAmap[x, y]) continue;
                    Vector2Int coord = new Vector2Int(x, y);
                    children[coord] = new List<Vector2Int>();
                    inDegree[coord] = 0;
                }
            }

            foreach (var kv in parentMap)
            {
                Vector2Int child = kv.Key;
                Vector2Int parent = kv.Value;
                children[parent].Add(child);
                inDegree[parent]++;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, int> weight = new Dictionary<Vector2Int, int>();
            foreach (var kv in inDegree)
            {
                if (kv.Value == 0)
                {
                    queue.Enqueue(kv.Key);
                    weight[kv.Key] = 1;
                }
            }

            while (queue.Count > 0)
            {
                Vector2Int node = queue.Dequeue();

                if (parentMap.TryGetValue(node, out Vector2Int parent))
                {
                    int candidate = weight[node] + 1;
                    if (!weight.ContainsKey(parent) || weight[parent] < candidate)
                    {
                        weight[parent] = candidate;
                    }

                    inDegree[parent]--;
                    if (inDegree[parent] == 0)
                    {
                        queue.Enqueue(parent);
                    }
                }
            }

            int[,] result = new int[res, res];
            foreach (var kv in weight)
            {
                result[kv.Key.x, kv.Key.y] = kv.Value;
            }

            return result;
        }
        public static int[,] CalculateWeights(Vector2Int[,] dirMap)
        {
            int res = dirMap.GetLength(0);

            Dictionary<Vector2Int, List<Vector2Int>> children = new Dictionary<Vector2Int, List<Vector2Int>>();
            Dictionary<Vector2Int, int> inDegree = new Dictionary<Vector2Int, int>();

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    if (dirMap[x, y] == SENTINEL) continue;
                    Vector2Int coord = new Vector2Int(x, y);
                    children[coord] = new List<Vector2Int>();
                    inDegree[coord] = 0;
                }
            }

            foreach (var kv in children)
            {
                Vector2Int child = kv.Key;
                Vector2Int offset = dirMap[child.x,child.y];
                if (offset == Vector2Int.zero) continue;
                Vector2Int parent = new Vector2Int(child.x + offset.x, child.y + offset.y);
                if (!children.ContainsKey(parent)) continue;

                children[parent].Add(child);
                inDegree[parent] = inDegree[parent] + 1;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, int> weight = new Dictionary<Vector2Int, int>();
            foreach (var kv in inDegree)
            {
                if (kv.Value == 0)
                {
                    queue.Enqueue(kv.Key);
                    weight[kv.Key] = 1;
                }
            }

            while (queue.Count > 0)
            {

                Vector2Int node = queue.Dequeue();
                Vector2Int dir = dirMap[node.x, node.y];

                if (dir == SENTINEL)  continue;
                if (dir == Vector2Int.zero) continue;

                Vector2Int parent = new Vector2Int(node.x + dir.x, node.y + dir.y);
                if (!weight.ContainsKey(parent) || weight[parent] < weight[node] + 1)
                {
                    weight[parent] = weight[node] + 1;
                }

                inDegree[parent] = inDegree[parent] - 1;
                if (inDegree[parent] == 0)
                {
                    queue.Enqueue(parent);
                }
            }

            int[,] result = new int[res, res];
            foreach (var kv in weight)
            {
                result[kv.Key.x, kv.Key.y] = kv.Value;
            }

            return result;
        }
        public static float[,] ApplySmoothHeights(int[,] weights, float smoothPower = 0.5f, bool power = true)
        {
            int res = weights.GetLength(0);
            float[,] heights = new float[res, res];
            int maxWeight = 0;
            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    if (weights[x, y] > maxWeight)
                    {
                        maxWeight = weights[x, y];
                    }
                }
            }
            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    float normWeight = weights[x, y] / (float)maxWeight;

                    heights[x, y] = power ? Mathf.Pow(normWeight, smoothPower) : 1 - (1 / (1 + normWeight));
                }
            }
            return heights;
        }
        public static int[] ComputeLevels(int baseSize, int finalSize)
        {
            List<int> levels = new List<int>();
            int current = baseSize;
            levels.Add(current);
            while (current * 2 <= finalSize)
            {
                current *= 2;
                levels.Add(current);
            }
            return levels.ToArray();
        }
        public static float[,] UpscaleBilinear(float[,] map, int newSize)
        {
            int oldSize = map.GetLength(0);
            float[,] result = new float[newSize, newSize];
            float scale = (oldSize - 1f) / (newSize - 1f);

            for (int i = 0; i < newSize; i++)
            {
                for (int j = 0; j < newSize; j++)
                {
                    float mapPosX = i * scale;
                    float mapPosY = j * scale;
                    int x0 = Mathf.FloorToInt(mapPosX);
                    int y0 = Mathf.FloorToInt(mapPosY);
                    int x1 = Mathf.Min(x0 + 1, oldSize - 1);
                    int y1 = Mathf.Min(y0 + 1, oldSize - 1);

                    float fracOffsetX = mapPosX - x0;
                    float fracOffsetY = mapPosY - y0;

                    float topLeft = map[x0, y0];
                    float topRight = map[x1, y0];
                    float bottomLeft = map[x0, y1];
                    float bottomRight = map[x1, y1];

                    float topInterp = topLeft * (1 - fracOffsetX) + topRight * fracOffsetX;
                    float botInterp = bottomLeft * (1 - fracOffsetX) + bottomRight * fracOffsetX;

                    result[i, j] = topInterp * (1 - fracOffsetY) + botInterp * fracOffsetY;
                }
            }
            return result;
        }
        public static float[,] DualFilterBlur(float[,] map, int radius, float standardDeviation)
        {
            int size = map.GetLength(0);
            int half = size / 2;
            float[,] down = UpscaleBilinear(map, half);
            float[,] blurredDown = GaussianBlur(down, radius, standardDeviation);
            float[,] result = UpscaleBilinear(blurredDown, size);
            return result;
        }
        public static void MergeCrispIntoBlurry(bool[,] crispMap, float[,] crispHeight, float[,] blurry)
        {
            int newSize = blurry.GetLength(0);
            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    if (crispMap[x, y])
                    {
                        blurry[x, y] = crispHeight[x, y];
                    }
                }
            }
        }
        public static Vector2Int[,] UpscaleDirectionMap1(Vector2Int[,] map)
        {
            int oldSize = map.GetLength(0);
            int newSize = oldSize * 2;
            Vector2Int[,] newMap = new Vector2Int[newSize, newSize];
            for (int i = 0; i < newSize; i++)
            {
                for (int j = 0; j < newSize; j++)
                {
                    newMap[i, j] = SENTINEL;
                }
            }
            Vector2Int root = new Vector2Int(oldSize, oldSize);

            void StepInDirection(Vector2Int oldPos, Vector2Int newPos)
            {
                Vector2Int dir = map[oldPos.x, oldPos.y];
                newMap[newPos.x, newPos.y] = dir;
                Vector2Int lastPos = newPos - dir;
                newMap[lastPos.x, lastPos.x] = dir;
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        Vector2Int walk = map[x + lastPos.x, y + lastPos.y];
                        if (walk == SENTINEL || (walk.x != -x && walk.y != -y)) continue;
                        if (x == 0 && y == 0) continue;
                        StepInDirection(
                            new Vector2Int(oldPos.x + x, oldPos.y + y),
                            new Vector2Int(lastPos.x + x, lastPos.y + y)
                        );
                    }
                }
            }

            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (map[x, y] == SENTINEL) continue;
                    if (x == 0 && y == 0) continue;
                    StepInDirection(
                        new Vector2Int(root.x / 2 + x, root.y / 2 + y),
                        new Vector2Int(root.x + x, root.y + y)
                    );
                }
            }
            return newMap;
        }

        public static Vector2Int[,] UpscaleDirectionMap(Vector2Int[,] map, int newSize, int jitterRange = 0)
        {
            int oldSize = map.GetLength(0);
            int scaleFactor = newSize / oldSize;
            if (newSize % oldSize != 0)
            {
                Debug.LogError("error in upscale, size isnt right");
            }

            Vector2Int[,] newMap = new Vector2Int[newSize, newSize];
            for (int i = 0; i < newSize; i++)
            {
                for (int j = 0; j < newSize; j++)
                {
                    newMap[i, j] = SENTINEL;
                }
            }

            for (int x = 0; x < oldSize; x++)
            {
                for (int y = 0; y < oldSize; y++)
                {
                    Vector2Int oldOffset = map[x, y];
                    if (oldOffset == SENTINEL) continue;

                    int childX = x * scaleFactor;
                    int childY = y * scaleFactor;

                    if (oldOffset == Vector2Int.zero)
                    {
                        newMap[childX, childY] = Vector2Int.zero;
                        continue;
                    }

                    int parentX = childX + oldOffset.x * scaleFactor;
                    int parentY = childY + oldOffset.y * scaleFactor;

                    int midX = (childX + parentX) / 2;
                    int midY = (childY + parentY) / 2;

                    int maxAllowedJitter = scaleFactor / 2;
                    int actualJitter = jitterRange > maxAllowedJitter ? maxAllowedJitter : jitterRange;

                    int jitterX = 0;
                    int jitterY = 0;
                    if (actualJitter > 0)
                    {
                        lock (rndLock)
                        {
                            jitterX = globalRnd.Next(-actualJitter, actualJitter + 1);
                            jitterY = globalRnd.Next(-actualJitter, actualJitter + 1);
                        }
                    }

                    int jitteredMidX = Mathf.Clamp(midX + jitterX, 0, newSize - 1);
                    int jitteredMidY = Mathf.Clamp(midY + jitterY, 0, newSize - 1);

                    newMap[childX, childY] = new Vector2Int(
                        jitteredMidX - childX,
                        jitteredMidY - childY
                    );

                    jitterOffsets.Add(new Vector2Int(jitteredMidX, jitteredMidY));

                    int clampedParentX = Mathf.Clamp(parentX, 0, newSize - 1);
                    int clampedParentY = Mathf.Clamp(parentY, 0, newSize - 1);
                    newMap[jitteredMidX, jitteredMidY] = new Vector2Int(
                        clampedParentX - jitteredMidX,
                        clampedParentY - jitteredMidY
                    );
                }
            }

            return newMap;
        }


        public static List<Vector2Int> BresenhamLine(Vector2Int a, Vector2Int b)
        {
            List<Vector2Int> result = new List<Vector2Int>();

            int x0 = a.x, y0 = a.y;
            int x1 = b.x, y1 = b.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = (x0 < x1) ? 1 : -1;
            int sy = (y0 < y1) ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                result.Add(new Vector2Int(x0, y0));
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return result;
        }
        public static bool[,] BuildMapFromDirections(Vector2Int[,] upscaledDir)
        {

            int newSize = upscaledDir.GetLength(0);
            Debug.Log(newSize);
            bool[,] boolMap = new bool[newSize, newSize];

            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    boolMap[x, y] = upscaledDir[x, y] != SENTINEL;
                }
            }

            return boolMap;
        }
        private static void SubdivideSegment(Vector2 start, Vector2 end, float amplification, System.Random rnd, List<Vector2> outputVertices)
        {
            if (amplification < 1f || Vector2.Distance(start, end) < 2f)
            {
                outputVertices.Add(end);
                return;
            }

            Vector2 mid = (start + end) * 0.5f;

            float jitterX = (float)((rnd.NextDouble() * 2.0 - 1.0) * amplification);
            float jitterY = (float)((rnd.NextDouble() * 2.0 - 1.0) * amplification);
            Vector2 midJittered = new Vector2(mid.x + jitterX, mid.y + jitterY);

            SubdivideSegment(start, midJittered, amplification * 0.5f, rnd, outputVertices);
            SubdivideSegment(midJittered, end, amplification * 0.5f, rnd, outputVertices);
        }








    }


}
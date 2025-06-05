using System;
using System.Collections.Generic;
using UnityEngine;

namespace DLA
{
    static class Utils
    {
        private static readonly object rndLock = new object();
        private static readonly System.Random globalRnd = new System.Random();
        public static List<Vector2Int> jitterOffsets = new List<Vector2Int>();
        public static readonly Vector2Int SENTINEL = new Vector2Int(int.MinValue, int.MinValue);
        private static readonly object jitterLock = new object();
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
        public static void ClearJitterOffsets()
        {
            lock (jitterLock)
            {
                jitterOffsets.Clear();
            }
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

                    if (point < minH)
                    {
                        minH = point;
                    }
                    if (point > maxH)
                    {
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
        public static int[,] CalculateWeights(Vector2Int[,] dirMap)
        {
            int res = dirMap.GetLength(0);

            // list of children each cell has
            Dictionary<Vector2Int, List<Vector2Int>> children = new Dictionary<Vector2Int, List<Vector2Int>>();

            //children count in each cell
            Dictionary<Vector2Int, int> inDegree = new Dictionary<Vector2Int, int>();


            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    // init dics 
                    if (dirMap[x, y] == SENTINEL) continue;
                    Vector2Int coord = new Vector2Int(x, y);
                    children[coord] = new List<Vector2Int>();
                    inDegree[coord] = 0;
                }
            }

            foreach (var kv in children)
            {
                // set children and parents 
                Vector2Int child = kv.Key;
                Vector2Int offset = dirMap[child.x, child.y];

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
                    // if its a leaf, add it to a queue 
                    queue.Enqueue(kv.Key);
                    weight[kv.Key] = 1;
                }
            }

            while (queue.Count > 0)
            {
                // run breath first search 
                Vector2Int node = queue.Dequeue();
                Vector2Int dir = dirMap[node.x, node.y];

                if (dir == SENTINEL) continue;
                if (dir == Vector2Int.zero) continue;

                Vector2Int parent = new Vector2Int(node.x + dir.x, node.y + dir.y);
                if (!weight.ContainsKey(parent) || weight[parent] < weight[node] + 1)
                {
                    //set values of parents so it goes up the closet to the center it is
                    weight[parent] = weight[node] + 1;
                }

                inDegree[parent] = inDegree[parent] - 1;
                if (inDegree[parent] == 0)
                {
                    // add to the queue and repeat
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
            if (power)
            {
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
            }
            // finds highest weight
            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    float normWeight = weights[x, y] / (float)maxWeight;

                    heights[x, y] = power ? Mathf.Pow(normWeight, smoothPower) : 1 - (1 / (1 + weights[x, y]));
                }
            }
            return heights;
        }

        // originally i was using this but its been reworked
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

        public static List<int> ComputeLevels(int baseSize, int finalSize)
        {
            List<int> levels = new List<int>();
            int current = baseSize;
            levels.Add(current);
            while (current * 2 <= finalSize)
            {
                current *= 2;
                levels.Add(current);
            }
            return levels;
        }

        public static float[,] UpscaleNearestNeighbor(float[,] source)
        {

            int oldSize = source.GetLength(0);
            int newSize = oldSize * 2;
            float[,] result = new float[newSize, newSize];

            for (int x = 0; x < oldSize; x++)
            {
                for (int y = 0; y < oldSize; y++)
                {
                    float v = source[x, y];
                    int newX = x * 2;
                    int newY = y * 2;
                    result[newX, newY] = v;
                    result[newX + 1, newY] = v;
                    result[newX, newY + 1] = v;
                    result[newX + 1, newY + 1] = v;
                }
            }

            return result;
        }
        public static float[,] UpscaleAndBlur(float[,] source,int blurRadius,float blurSigma)
        {
            float[,] upscaled = UpscaleNearestNeighbor(source);

            float[,] blurred = upscaled;
            blurred = GaussianBlur(blurred, blurRadius, blurSigma);

            return blurred;
        }

        public static void UpscaleCrisp(Vector2Int[,] parentMap, int[,] depthMap, bool[,] CrispMap, out Vector2Int[,] newParentMap, out int[,] newDepthMap, out bool[,] newCrispMap)
        {
            int oldSize = parentMap.GetLength(0);
            int newSize = oldSize * 2;

            newParentMap = new Vector2Int[newSize, newSize];
            newDepthMap = new int[newSize, newSize];
            newCrispMap = new bool[newSize, newSize];
            //init arrays
            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    newParentMap[x, y] = SENTINEL;
                    newDepthMap[x, y] = -1;
                    newCrispMap[x, y] = false;
                }
            }

            for (int x = 0; x < oldSize; x++)
            {
                for (int y = 0; y < oldSize; y++)
                {
                    Vector2Int dir = parentMap[x, y];

                    if (depthMap[x, y] < 0) continue;

                    int oldDepth = depthMap[x, y];
                    int bigX = x * 2;
                    int bigY = y * 2;

                    //copy into array 
                    newParentMap[bigX, bigY] = dir;
                    newDepthMap[bigX, bigY] = oldDepth;
                    newCrispMap[bigX, bigY] = true;
                    bool isRoot = (dir.x == 0 && dir.y == 0);
                    if (!isRoot)
                    {
                        int parentX = x + dir.x;
                        int parentY = y + dir.y;

                        if (parentX >= 0 && parentX < oldSize && parentY >= 0 && parentY < oldSize)
                        {
                            int newParentX = parentX * 2;
                            int newParentY = parentY * 2;
                            // step in direction 
                            int stepX = Math.Sign(newParentX - bigX);
                            int stepY = Math.Sign(newParentY - bigY);
                            // add new point
                            int midX = bigX + stepX;
                            int midY = bigY + stepY;

                            // place the midpoint
                            newParentMap[midX, midY] = new Vector2Int(
                                Math.Sign(newParentX - midX),
                                Math.Sign(newParentY - midY)
                            );
                            newDepthMap[midX, midY] = oldDepth + 1;
                            newCrispMap[midX,midY] = true;
                        }
                    }
                }
            }
        }
        public static float[,] LerpMaps(float[,] mapA, float[,] mapB, float t)
        {
            int size = mapA.GetLength(0);
            if (size != mapB.GetLength(0))
            {
                Debug.Log("map a is not the same size as mab b");
            }

            float[,] result = new float[size,size];

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    result[x,y] = (mapB[x,y] - mapA[x,y]) * t + mapA[x,y];
                    //pretty sure mathf.lerp breaks at task time, this takes like 2 seconds to do so
                }
            }
            return result;
        }
    }


}
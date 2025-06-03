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

        public static float[,] ApplySmoothHeights(int[,] weights, float smoothPower = 0.5f)
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

                    heights[x, y] = Mathf.Pow(normWeight, smoothPower);
                    //heights[x, y] = 1 - (1 / (1 + normWeight));
                }
            }
            return heights;
        }
        public static int[] ComputeLevels(int baseSize, int finalSize)
        {
            List<int> levels = new List<int>();
            int current = baseSize;
            levels.Add(current);
            while (current*2 <= finalSize)
            {
                current *= 2;
                levels.Add(current);
            }
            return levels.ToArray();
        }
        public static bool[,] UpscaleNearest(bool[,] map, int newSize)
        {
            int oldSize = map.GetLength(0);
            bool[,] result = new bool[newSize, newSize];
            float scale = (float)oldSize / newSize;

            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    int mapX = Mathf.FloorToInt(x * scale);
                    int mapY = Mathf.FloorToInt(y * scale);
                    result[x, y] = map[mapX, mapY];
                }
            }
            return result;
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
        public static void MergeCrispIntoBlurry(bool[,] crispNN, float[,] crispHeight, float[,] blurry)
        {
            int newSize = blurry.GetLength(0);
            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    if (crispNN[x, y])
                    { 
                        blurry[x, y] = crispHeight[x, y]; 
                    }
                }
            }
        }

        public static Vector2Int[,] BuildDirectionMap(bool[,] oldBoolMap, Dictionary<Vector2Int, Vector2Int> parentDict)
        {
            int oldSize = oldBoolMap.GetLength(0);
            Vector2Int[,] dirMap = new Vector2Int[oldSize, oldSize];

            for (int i = 0; i < oldSize; i++)
            {
                for (int j = 0; j < oldSize; j++)
                {
                    dirMap[i, j] = SENTINEL;
                }
            }
            // init to impossible value 

            foreach (var kv in parentDict)
            {
                Vector2Int child = kv.Key;
                Vector2Int parent = kv.Value;
                Vector2Int offset = parent - child;
                dirMap[child.x, child.y] = offset;
                //gets offset
            }

            for (int x = 0; x < oldSize; x++)
            {
                for (int y = 0; y < oldSize; y++)
                {
                    if (oldBoolMap[x, y] && !parentDict.ContainsKey(new Vector2Int(x, y)))
                    {
                        dirMap[x, y] = Vector2Int.zero;
                        //sets root dir
                    }
                }
            }

            return dirMap;
        }
        public static Vector2Int[,] UpscaleDirectionMap(Vector2Int[,] oldDir,int newSize)
        {
            int oldSize = oldDir.GetLength(0);
            Vector2Int[,] newDir = new Vector2Int[newSize, newSize];
            float scale = (float)oldSize / newSize;

            for (int x = 0; x < newSize; x++)
            {
                for (int y = 0; y < newSize; y++)
                {
                    int oldX = Mathf.FloorToInt(x * scale);
                    int oldY = Mathf.FloorToInt(y * scale);
                    // scales map
                    newDir[x, y] = oldDir[oldX, oldY];
                }
            }

            return newDir;
        }
        public static bool[,] BuildMapFromDirections(Vector2Int[,] upscaledDir)
        {
            int newSize = upscaledDir.GetLength(0);
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

    }


}
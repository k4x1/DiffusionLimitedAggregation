using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DLA
{
    static class Utils
    {
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

                    if (point < minH) minH = point;
                    if (point > maxH) maxH = point;
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
        public static int[,] ComputeWeightMap(bool[,] map)
        {
            int res = map.GetLength(0); 
            int[,] weights = new int[res, res];

            Vector2Int[] directions = new Vector2Int[] {
                new Vector2Int(1,  0), // E
                new Vector2Int(-1, 0), // W
                new Vector2Int(0,  1), // N 
                new Vector2Int(0, -1)  // S
            };

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    if (!map[x, y]) continue;
                    int canditate = 0;
                    foreach (Vector2Int dir in directions)
                    {
                        int nx = x + dir.x;
                        int ny = y + dir.y;
                        if (nx < 0 || nx >= res || ny < 0 || ny >= res || !map[nx, ny])
                        {
                            continue;   
                        }
                        Debug.Log(weights[nx, ny]);
                        canditate = canditate < weights[nx,ny] ? weights[nx,ny] : canditate;
                    }
                    weights[x,y] = ++canditate;
                }
            }

            return weights;
        }
        public static int[,] ComputeWeightMap1(Vector2Int[,] map, bool[,] DLAMap)
        {
            int res = map.GetLength(0);
            int[,] weights = new int[res, res];
            for (int i = 0; i < res; i++){
                for (int j = 0; j < res; j++)
                {
                    weights[i, j] = -1; 
                }
            }
            int Compute(int x, int y)
            {

                if (!DLAMap[x, y])
                {
                    return weights[x, y] = 0; 
                }

                if (weights[x, y] >= 0)
                {
                    return weights[x, y];
                }
                Vector2Int dir = map[x, y];
                if (dir.x < 0 || dir.y < 0)
                {
                    return weights[x, y] = 0;
                }
                int newWeight = Compute(dir.x, dir.y) + 1;
                weights[x, y] = newWeight;
                return newWeight;
            }

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    weights[x, y] = Compute(x, y);

                } 
            }
            return weights;
        }
        public static int[,] CalculateWeights(bool[,] DLAmap, Dictionary<Vector2Int, Vector2Int> parentMap)
        {
            int res = DLAmap.GetLength(0);

            Dictionary<Vector2Int, List<Vector2Int>> children = new Dictionary<Vector2Int, List<Vector2Int>>();
            Dictionary<Vector2Int, int> inDegree = new Dictionary<Vector2Int, int>();

            for (int x = 0; x < res; x++) { 
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

        public static float[,] ApplySmoothHeights(int[,] weights)
        {
            int res = weights.GetLength(0);
            float[,] heights = new float[res,res];
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

                    heights[x, y] = 1 - (1 / (1 + normWeight));
                }
            }
            return heights;
        }
    }
}
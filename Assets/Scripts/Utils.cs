using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Callbacks;
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
            Queue<Vector2Int> boundaryQueue = new Queue<Vector2Int>();

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
                    if(!map[x, y]) continue;
                    bool isBoundary = x == 0 || y == 0 || x == res - 1 || y == res - 1;
                    for (int i = 0; !isBoundary && i < directions.Length; i++)
                    {
                        Vector2Int dir = directions[i];
                        int newX = x + dir.x;
                        int newY = y + dir.y;
                        if (newX < 0 || newX >= res || newY < 0 || newY >= res || !map[newX, newY])
                        {
                            isBoundary = true;
                        }
                    }

                    if (isBoundary)
                    {
                        weights[x, y] = 1;
                        boundaryQueue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }
            while (boundaryQueue.Count > 0)
            {
                Vector2Int popped = boundaryQueue.Dequeue();
                int w = weights[popped.x, popped.y];
                foreach(Vector2Int dir in directions)
                {
                    int newX = popped.x + dir.x;
                    int newY = popped.y + dir.y;
                    if (newX < 0 || newX >= res || newY < 0 || newY >= res) continue;

                    if (!map[newX, newY]) continue;

                    if (weights[newX, newY] < w + 1)
                    {
                        weights[newX, newY] = w + 1;
                        boundaryQueue.Enqueue(new Vector2Int(newX, newY));
                    }
                }
            }

            return weights;
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
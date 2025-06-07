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
        public static float[,] InitializeNoiseField(int size, float noiseScale)
        {
            float[,] noiseField = new float[size, size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float sampleX = (float)x / size * noiseScale;
                    float sampleY = (float)y / size * noiseScale;

                    noiseField[x, y] = Mathf.PerlinNoise(sampleX, sampleY);
                }
            }
            return noiseField;
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
                }
            }
            return result;
        }
        private static int Cross(Vector2Int origin, Vector2Int pointA, Vector2Int pointB)
        {
            return (pointA.x - origin.x) * (pointB.y - origin.y)
                 - (pointA.y - origin.y) * (pointB.x - origin.x);
        }


        public static List<Vector2Int> ConvexHull(List<Vector2Int> points)
        {

            if (points.Count <= 1) return new List<Vector2Int>(points);

            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            

            List<Vector2Int> lower = new List<Vector2Int>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector2Int point = points[i];
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0)
                {
                    lower.RemoveAt(lower.Count - 1);
                }
                lower.Add(point);
            }

            List<Vector2Int> upper = new List<Vector2Int>();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2Int point = points[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0)
                {
                    upper.RemoveAt(upper.Count - 1);
                }
                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }
        public static List<Vector2Int> RefineHull(List<Vector2Int> hull, List<Vector2Int> allPts, float maxGap)
        {
            bool inserted;
            do
            {
                inserted = false;
                List<Vector2Int> newHull = new List<Vector2Int>();

                for (int i = 0; i < hull.Count; i++)
                {
                    Vector2Int pointA = hull[i];
                    Vector2Int pointB = hull[(i + 1) % hull.Count];
                    newHull.Add(pointA);

                    float edgeLen = Vector2Int.Distance(pointA, pointB);
                    if (edgeLen > maxGap)
                    {
                        float bestDist = 0f;
                        Vector2Int bestPoint = default;
                        Vector2 edge = (pointB - pointA).ToVector2();
                        for (int j = 0; j < allPts.Count; j++)
                        {
                            Vector2Int point = allPts[j];
                            Vector2 toPoint = (point - pointA).ToVector2();
                            float t = Vector2.Dot(toPoint, edge) / edge.sqrMagnitude;
                            if (t <= 0 || t >= 1) continue;
                            Vector2 proj = pointA.ToVector2() + edge * t;
                            float currDistance = Vector2.Distance(point.ToVector2(), proj);
                            if (currDistance > bestDist)
                            {
                                bestDist = currDistance;
                                bestPoint = point;
                            }
                        }

                        if (bestDist > 0f && Vector2Int.Distance(pointA, bestPoint) <= maxGap && Vector2Int.Distance(bestPoint, pointB) <= maxGap)
                        {
                            newHull.Add(bestPoint);
                            inserted = true;
                            Debug.Log("refined");
                        }
                    }
                }
                hull = newHull;
            } while (inserted);
            return hull;
        }
        
        
        public static List<Vector2Int> ScalePolygon(List<Vector2Int> polygon, float scale)
        {

            Vector2 center = Vector2.zero;
            foreach (Vector2Int point in polygon) 
            { 
                center += point; 
            }
            center /= polygon.Count;

            List<Vector2Int> outPoly = new List<Vector2Int>(polygon.Count);
            foreach (Vector2 point in polygon)
            {
                Vector2 offset = point - center;
                Vector2 newPoint = center + offset * scale;
                outPoly.Add(ToVector2Int(newPoint));
            }
            return outPoly;
        }
        public static int EuclidsAlgorithm (int a, int b)
        {
            // finds the greatest common dividor of 2 values
            return b == 0 ? a : EuclidsAlgorithm(b, a % b);
        }
        static Vector2 ToVector2(this Vector2Int vec) => new Vector2(vec.x, vec.y);
        static Vector2Int ToVector2Int(this Vector2 vec) => new Vector2Int(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
    }

}
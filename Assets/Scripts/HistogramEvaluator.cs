// Ignore Spelling: Json

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
namespace DLA
{
    [ExecuteAlways]
    public class HistogramEvaluator : MonoBehaviour
    {
        //source https://tangrams.github.io/heightmapper/#2.315/2.38/47.21
        public Texture2D heighMapA;
        public Texture2D heightMapB;
        public Texture2D[] realHeightMapList;
        public double result = 0;
        public float[,] genAsFloat;
        public float[,] realAsFloat;

        [Header("heightmap json")]
        public TextAsset heightmapJson;
        [HideInInspector] public float[,] loadedHeightMap;

        [Header("Gizmo Settings")]
        public float gizmoBarWidth = 0.1f;
        public float gizmoMaxHeight = 5f;
        public float histogramSpacing = 1f;


        public double CompareHeightsChi()
        {

            float[,] gen = LoadHeightmap(heighMapA);
            float[,] real = LoadHeightmap(heightMapB);
            int[] hisGen = ComputeHistogram(gen);
            int[] hisReal = ComputeHistogram(real);

            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);
            result = ChiSquared(normGen, normReal);
            Debug.Log($"Chi results {result}");
            return result;
        }
        public double CompareHeightsCoefficient()
        {
            float[,] gen = LoadHeightmap(heighMapA);
            float[,] real = LoadHeightmap(heightMapB);
            int[] hisGen = ComputeHistogram(gen);
            int[] hisReal = ComputeHistogram(real);

            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);

            result = CorrelationCoefficient(normGen, normReal);
            Debug.Log($"Coefficient results {result}");
            return result;
        }
        public double[] CompareJsonToRealListChi()
        {
            if (loadedHeightMap == null) LoadMapJson();

            int[] histJson = ComputeHistogram(loadedHeightMap);
            float[] normJson = NormalizeHistogram(histJson);

            double[] results = new double[realHeightMapList.Length];
            double average = 0;
            for (int i = 0; i < realHeightMapList.Length; i++)
            {
                Texture2D realTex = realHeightMapList[i];
                if (realTex == null)
                {
                    Debug.LogWarning($"realHeightMapList[{i}] is null");
                    results[i] = double.NaN;
                    continue;
                }

                float[,] realMap = LoadHeightmap(realTex);
                int[] histReal = ComputeHistogram(realMap);
                float[] normReal = NormalizeHistogram(histReal);

                double chi = ChiSquared(normJson, normReal);
                average += chi;
                results[i] = chi;
                //Debug.Log($"JSON vs {realTex.name}: chi = {chi}");
            }
            average = average / realHeightMapList.Length;
            result = average;
            string line = ($"Average chisqr score = {average}");
            Debug.Log(line);
            Utils.Log(line);
            return results;
        }

        public double[] CompareJsonToRealListCoefficient()
        {
            if (loadedHeightMap == null) LoadMapJson();

            int[] histJson = ComputeHistogram(loadedHeightMap);
            float[] normJson = NormalizeHistogram(histJson);

            double[] results = new double[realHeightMapList.Length];
            double average = 0;
            for (int i = 0; i < realHeightMapList.Length; i++)
            {
                Texture2D realTex = realHeightMapList[i];
                if (realTex == null)
                {
                    Debug.LogWarning($"realHeightMapList[{i}] is null");
                    results[i] = double.NaN;
                    continue;
                }

                float[,] realMap = LoadHeightmap(realTex);
                int[] histReal = ComputeHistogram(realMap);
                float[] normReal = NormalizeHistogram(histReal);

                double coeff = CorrelationCoefficient(normJson, normReal);
                average += coeff;
                results[i] = coeff;
               // Debug.Log($"JSON vs {realTex.name}: coefficient = {coeff}");
            }
            average = average / realHeightMapList.Length;
            result = average;
            string line = ($"Average coefficient score = {average}");
            Debug.Log(line);
            Utils.Log(line);
            return results;
        }
      
        public void LoadMapJson()
        {
            if (heightmapJson == null)
            {
                Debug.LogError("no json file assigned");
                return;
            }

            DLADataJson container;
            try
            {
                container = JsonUtility.FromJson<DLADataJson>(heightmapJson.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"failed to read file {e.Message}");
                return;
            }

            int size = container.size;
            if (container.heightMapData == null || container.heightMapData.Length != size * size)
            {
                Debug.LogError($"unexpected length, expected {size * size}, got {container.heightMapData?.Length}");
                return;
            }

            loadedHeightMap = new float[size, size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    int i = x * size + y;
                    loadedHeightMap[x, y] = container.heightMapData[i];
                }
            }

          //  Debug.Log($"loaded height map size ({size}x{size})");
        }


        private void DrawHistogramGizmos(float[] histogram, Vector3 origin, Color color)
        {
            Gizmos.color = color;
            int binCount = histogram.Length;
            float maxCount = histogram.Max();

            for (int i = 0; i < binCount; i++)
            {
                float normalized = maxCount > 0 ? histogram[i] / maxCount : 0f;
                float barHeight = normalized * gizmoMaxHeight;
                Vector3 start = origin + new Vector3(i * gizmoBarWidth, 0f, 0f);
                Vector3 end = origin + new Vector3(i * gizmoBarWidth, barHeight, 0f);
                Gizmos.DrawLine(start, end);
            }
        }
 
        float[,] LoadHeightmap(Texture2D map, int res = 513)
        {

            if (map == null)
            {
                Debug.LogError("no texture");
                return null;
            }

            if (map.width != map.height)
            {
                Debug.LogWarning($"source not square {map.name}");
            }
            float[,] result = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                float v = (y + 0.5f) / res;

                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;

                    Color sampledColor = map.GetPixelBilinear(u, v);

                    float gray = sampledColor.r;

                    result[x, y] = gray;
                }
            }

            return result;
        }
    
        private float[] NormalizeHistogram(int[] histogram)
        {
            float total = histogram.Sum();
            if (total <= 0) return histogram.Select(_ => 0f).ToArray();
            return histogram.Select(bin => bin / total).ToArray();
        }
        List<float> FlattenHeights(float[,] map)
        {
            List<float> result = new List<float>();

            for (int x = 0; x < map.GetLength(0); x++)
            {
                for (int y = 0; y < map.GetLength(1); y++)
                {
                    result.Add(map[x, y]);
                }

            }
            return result;
        }
        int[] ComputeHistogram(float[,] map)
        {
            List<float> heights = new List<float>();
            
            heights = FlattenHeights((map));

            float minH = heights.Min();
            float maxH = heights.Max();

            int binCount = 256;

            float binWidth = (maxH - minH) / binCount;

            int[] histogram = new int[binCount];
            foreach (float h in heights)
            {
                int index = Mathf.Clamp((int)((h - minH) / binWidth), 0, binCount - 1);
                histogram[index]++;
            }

            return histogram;

        }

        double ChiSquared(float[] generated, float[] real)
        {
            /*
                               = chisqr = 
                        sum(squared(generated-real)
                                /real)
             */

            double chisqr = 0;
            for (int i = 0; i < generated.Length; i++)
            {
                if (real[i] > 0)
                {
                    double d = generated[i] - real[i];
                    chisqr += (d * d) / real[i];
                }
            }
            return chisqr;
        }
        double CorrelationCoefficient(float[] generated, float[] real)
        {
            /*
                                                      = coefficient = 
                            sum((generated - generatedMeanBin) * (real - realMeanBin)) 
                                                           / 
                sqrt(sum(squared(generated - generatedMeanBin))) * sum(squared(real - realMeanBin)))
             */

            int binCount = generated.Length;

            double sumGen = generated.Sum();
            double sumReal = real.Sum();

            double genMeanBin = sumGen / binCount;
            double realMeanBin = sumReal / binCount;

            double covarianceSum = 0;
            double varianceGen = 0;
            double varianceReal = 0;

            for (int i = 0; i < binCount; i++)
            {
                double dGen = generated[i] - genMeanBin;
                double dReal = real[i] - realMeanBin;
                covarianceSum += dGen * dReal;
                varianceGen += dGen * dGen;
                varianceReal += dReal * dReal;
            }
            return covarianceSum / Math.Sqrt(varianceGen * varianceReal);
        }

        public static float[,] ConvertTextureToFloatArray(Texture2D tex, int size)
        {
            if (tex == null)
            {
                Debug.LogError("null text");
                return null;
            }

            if (size <= 0)
            {
                Debug.LogError("tex size doesn't match");
                return null;
            }

            if (tex.width != tex.height)
            {
                Debug.LogWarning("tex not square");
            }

            float[,] result = new float[size, size];

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    Color sampledColor = tex.GetPixelBilinear(u, v);

                    float gray = sampledColor.r;

                    result[x, y] = gray;
                }
            }

            return result;
        }
        float[,] NormalizeMap(float[,] map)
        {
            int size = map.GetLength(0);
            float min = map.Cast<float>().Min();
            float max = map.Cast<float>().Max();

            float range = Mathf.Max(max - min, 1e-6f);

            float[,] norm = new float[size, size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    norm[x, y] = (map[x, y] - min) / range;
                }
            }
            return norm;
        }

        private void OnDrawGizmos()
        {
     /*       if (heighMapA == null || heightMapB == null) return;

            genAsFloat = LoadHeightmap(heighMapA);
            realAsFloat = LoadHeightmap(heightMapB);
            int[] hisGen = ComputeHistogram(genAsFloat);
            int[] hisReal = ComputeHistogram(realAsFloat);
            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);

            Vector3 originGen = transform.position;
            DrawHistogramGizmos(normGen, originGen, Color.red);

            Vector3 originReal = transform.position + Vector3.right * (normGen.Length * gizmoBarWidth + histogramSpacing);
            DrawHistogramGizmos(normReal, originReal, Color.blue);*/
        }
    }
}
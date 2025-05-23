using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace DLA
{
    [ExecuteAlways]
    public class HistogramEvaluator : MonoBehaviour
    {
        //source https://tangrams.github.io/heightmapper/#2.315/2.38/47.21
        public Texture2D generatedHeightMap;
        public Texture2D realHeightMap;
        public double result = 0;
        public float[,] genAsFloat;
        public float[,] realAsFloat;

        [Header("Gizmo Settings")]
        public float gizmoBarWidth = 0.1f;
        public float gizmoMaxHeight = 5f;
        public float histogramSpacing = 1f;


        public double CompareHeightsChi()
        {

            float[,] gen = LoadHeightmap(generatedHeightMap);
            float[,] real = LoadHeightmap(realHeightMap);
            int[] hisGen = ComputeHistogram(gen);
            int[] hisReal = ComputeHistogram(real);

            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);
            result = ChiSquared(normGen, normReal);
            return result;
        }
        public double CompareHeightsCoefficient()
        {
            float[,] gen = LoadHeightmap(generatedHeightMap);
            float[,] real = LoadHeightmap(realHeightMap);
            int[] hisGen = ComputeHistogram(gen);
            int[] hisReal = ComputeHistogram(real);

            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);

            result = CorrelationCoefficient(normGen, normReal);
            return result;
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

        private void OnDrawGizmos()
        {
            if (generatedHeightMap == null || realHeightMap == null) return;

            genAsFloat = LoadHeightmap(generatedHeightMap);
            realAsFloat = LoadHeightmap(realHeightMap);
            int[] hisGen = ComputeHistogram(genAsFloat);
            int[] hisReal = ComputeHistogram(realAsFloat);
            float[] normGen = NormalizeHistogram(hisGen);
            float[] normReal = NormalizeHistogram(hisReal);

            Vector3 originGen = transform.position;
            DrawHistogramGizmos(normGen, originGen, Color.red);

            Vector3 originReal = transform.position + Vector3.right * (normGen.Length * gizmoBarWidth + histogramSpacing);
            DrawHistogramGizmos(normReal, originReal, Color.blue);
        }

        float[,] LoadHeightmap(Texture2D map)
        {
            float[,] result;

            int w = map.width;
            int h = map.height;

            result = new float[w, h];

            Color[] pixels = map.GetPixels();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = y * w + x;
                    result[x, y] = pixels[index].grayscale;
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

            heights = FlattenHeights(map);

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
    }
}
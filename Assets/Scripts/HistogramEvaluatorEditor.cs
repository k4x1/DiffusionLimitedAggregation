using UnityEditor;
using UnityEngine;
namespace DLA {
    [CustomEditor(typeof(HistogramEvaluator))]
    public class HistogramEvaluatorEditor : Editor
    {
        public override void OnInspectorGUI() 
        { 
            DrawDefaultInspector();

            HistogramEvaluator evaluatorRef = (HistogramEvaluator)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Compare Chi"))
            {
                evaluatorRef.CompareHeightsChi();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Compare Coefficient"))
            {
                evaluatorRef.CompareHeightsCoefficient();
            }    
         /*   EditorGUILayout.Space();
            if (GUILayout.Button("Draw Histograms"))
            {
                evaluatorRef.DrawHistograms();
            }*/
        }
    }
}
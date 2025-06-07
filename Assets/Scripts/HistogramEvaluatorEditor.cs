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
            if (GUILayout.Button("Compare Chi average"))
            {
                evaluatorRef.CompareJsonToRealListChi();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Compare Coefficient average"))
            {
                evaluatorRef.CompareJsonToRealListCoefficient();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Load json heightmap"))
            {
                evaluatorRef.LoadMapJson();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Compare Chi single"))
            {
                evaluatorRef.CompareHeightsChi();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Compare Coefficient single"))
            {
                evaluatorRef.CompareHeightsCoefficient();
            }
     
        }
    }
}
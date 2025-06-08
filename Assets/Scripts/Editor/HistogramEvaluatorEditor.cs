using UnityEditor;
using UnityEngine;
namespace DLA {
    [CustomEditor(typeof(HistogramEvaluator))]
    public class HistogramEvaluatorEditor : Editor
    {
        void OnSceneGUI()
        {
            HistogramEvaluator evaluatorRef = (HistogramEvaluator)target;
            if (evaluatorRef == null) return;

            
            Camera sceneCam = SceneView.currentDrawingSceneView.camera;
            Vector3 screenPos = sceneCam.WorldToScreenPoint(evaluatorRef.transform.position);
            if (screenPos.z > 0) {
                Handles.BeginGUI();
                Vector2 size = GUI.skin.label.CalcSize(new GUIContent($"Result: {evaluatorRef.result}"));
                Rect rect = new Rect(
                    screenPos.x - size.x/2,
                    SceneView.currentDrawingSceneView.position.height - screenPos.y - size.y,
                    size.x, size.y
                );
                GUI.Label(rect, $"Result: {evaluatorRef.result}");
                Handles.EndGUI();
            }
        }
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
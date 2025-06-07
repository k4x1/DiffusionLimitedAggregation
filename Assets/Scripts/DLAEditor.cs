using UnityEditor;
using UnityEngine;
namespace DLA
{
    [CustomEditor(typeof(DLA))]
    public class DLAEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            DLA dlaRef = (DLA)target;
            if (dlaRef.mode == TerrainMode.MultiResolutionDLA)
            {
                EditorGUILayout.HelpBox("base size for scaling, fill factor for how many walkers before increasing resolution", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fillFraction"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("crispBlurRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("crispBlurStandardDeviation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("blurryBlurRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("blurryBlurStandardDeviation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lerpAlpha"));
            }
            else if(dlaRef.mode == TerrainMode.BasicDLA)
            {
                EditorGUILayout.HelpBox("walker count is how many it starts with, max walkers is max clump amount", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("walkerCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWalkers"));
            }           
            else if(dlaRef.mode == TerrainMode.PerlinNoise)
            {
                EditorGUILayout.HelpBox("octaves is noise layer count, base scale is scale of first noise layer, " +
                    "persistance is how much it falls off, lacunarity is how much the frequency increases, seed is for rng", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("perlinOctaves"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("perlinBaseScale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("perlinPersistence"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("perlinLacunarity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("perlinSeed"));
            }
            else if (dlaRef.mode == TerrainMode.SimplexNoise)
            {
                EditorGUILayout.HelpBox("octaves is noise layer count, base scale is scale of first noise layer, " +
                    "persistance is how much it falls off, lacunarity is how much the frequency increases, seed is for rng", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("simplexOctaves"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("simplexBaseScale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("simplexPersistence"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("simplexLacunarity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("simplexSeed"));
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Run task DLA"))
            {
                dlaRef.StartTaskDLA();
            }           
            EditorGUILayout.Space();
            if (GUILayout.Button("Stop DLA"))
            {
                dlaRef.StopDLA();
            }     
            EditorGUILayout.Space();
            if (GUILayout.Button("Do post proccessing"))
            {
                dlaRef.PostProcessing();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

}

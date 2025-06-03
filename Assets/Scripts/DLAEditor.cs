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
            if (dlaRef.mode == DLAMode.MultiResolution)
            {
                EditorGUILayout.HelpBox("base size for scaling, fill factor for how many walkers before increasing resolution", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("baseSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fillFraction"));
            }
            else if(dlaRef.mode == DLAMode.Basic)
            {
                EditorGUILayout.HelpBox("walker count is how many it starts with, max walkers is max clump amount", MessageType.Info);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("walkerCount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWalkers"));
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
                dlaRef.PostProccessing();
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

}

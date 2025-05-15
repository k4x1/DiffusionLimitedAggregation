using UnityEditor;
using UnityEngine;
namespace DLA
{
    [CustomEditor(typeof(DLA))]
    public class DLAEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DLA dlaRef = (DLA)target;

            EditorGUILayout.Space();
            if (GUILayout.Button("Run DLA"))
            {
                dlaRef.StartDLA();
            EditorGUILayout.Space();
            }       
            if (GUILayout.Button("Stop DLA"))
            {
                dlaRef.StopDLA();
            }
        }
    }

}

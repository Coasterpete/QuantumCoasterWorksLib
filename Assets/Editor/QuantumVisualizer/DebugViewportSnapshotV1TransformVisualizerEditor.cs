using UnityEditor;
using UnityEngine;

namespace QuantumVisualizer.Editor
{
    [CustomEditor(typeof(DebugViewportSnapshotV1TransformVisualizer))]
    public sealed class DebugViewportSnapshotV1TransformVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var visualizer = (DebugViewportSnapshotV1TransformVisualizer)target;

            EditorGUILayout.Space(10f);
            DrawPrefabStatus(visualizer);

            EditorGUILayout.Space(8f);
            DrawGeneratedHierarchyActions(visualizer);
        }

        private static void DrawPrefabStatus(DebugViewportSnapshotV1TransformVisualizer visualizer)
        {
            EditorGUILayout.LabelField("Prefab Status", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Body Prefab", FormatAssignedStatus(visualizer.HasTrainBodyPrefab));
            EditorGUILayout.LabelField(
                "Banking Profile Prefab",
                FormatAssignedStatus(visualizer.HasBankingProfileBodyPrefab));
            EditorGUILayout.LabelField("Bogie Prefab", FormatAssignedStatus(visualizer.HasBogiePrefab));
            EditorGUILayout.LabelField("Wheel Prefab", FormatAssignedStatus(visualizer.HasWheelPrefab));
            EditorGUILayout.EndVertical();
        }

        private static void DrawGeneratedHierarchyActions(DebugViewportSnapshotV1TransformVisualizer visualizer)
        {
            EditorGUILayout.LabelField("Generated Hierarchy", EditorStyles.boldLabel);

            Transform generatedRoot = visualizer.FindGeneratedHierarchy();
            Transform bodyInstances = visualizer.FindTrainBodyInstances();
            Transform bankingProfileInstances = visualizer.FindBankingProfileBodyInstances();

            using (new EditorGUI.DisabledScope(generatedRoot == null))
            {
                if (GUILayout.Button("Select Generated Hierarchy", GUILayout.Height(28f)))
                {
                    SelectTransform(generatedRoot);
                }
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(bodyInstances == null || bodyInstances.childCount == 0))
            {
                if (GUILayout.Button("Select Body Instances", GUILayout.Height(28f)))
                {
                    SelectChildren(bodyInstances);
                }
            }

            using (new EditorGUI.DisabledScope(
                bankingProfileInstances == null || bankingProfileInstances.childCount == 0))
            {
                if (GUILayout.Button("Select Banking Profile Instances", GUILayout.Height(28f)))
                {
                    SelectChildren(bankingProfileInstances);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (generatedRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "Rebuild the generated boxes before selecting the generated hierarchy.",
                    MessageType.Info);
            }
        }

        private static string FormatAssignedStatus(bool assigned)
        {
            return assigned ? "Assigned" : "Missing";
        }

        private static void SelectTransform(Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            Selection.activeGameObject = transform.gameObject;
            EditorGUIUtility.PingObject(transform.gameObject);
        }

        private static void SelectChildren(Transform parent)
        {
            if (parent == null || parent.childCount == 0)
            {
                return;
            }

            var selectedObjects = new Object[parent.childCount];
            for (int i = 0; i < parent.childCount; i++)
            {
                selectedObjects[i] = parent.GetChild(i).gameObject;
            }

            Selection.objects = selectedObjects;
            Selection.activeGameObject = parent.GetChild(0).gameObject;
            EditorGUIUtility.PingObject(parent.GetChild(0).gameObject);
        }
    }
}

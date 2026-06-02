using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuantumVisualizer.Editor
{
    public sealed class DebugViewportSnapshotBrowserWindow : EditorWindow
    {
        private const string ViewerObjectName = "Quantum Snapshot Viewer";
        private const string MenuPath = "Window/Quantum/Snapshot Browser";

        private static readonly KnownSnapshotSample[] KnownSamples =
        {
            new KnownSnapshotSample("Built-in", "DebugViewportSnapshotV1.sample.json"),
            new KnownSnapshotSample("BankingProfile", "DebugViewportSnapshotV1.banking-profile.sample.json"),
            new KnownSnapshotSample("Straight Line", "Milestone7.synthetic.straight_line.snapshot.json"),
            new KnownSnapshotSample("Simple Hill", "Milestone7.synthetic.simple_hill.snapshot.json"),
            new KnownSnapshotSample("Banked Turn", "Milestone7.synthetic.banked_turn.snapshot.json"),
            new KnownSnapshotSample("Desc/Asc Curve", "Milestone7.synthetic.descending_ascending_curve.snapshot.json")
        };

        private readonly LocatedSnapshotSample[] _locatedSamples = new LocatedSnapshotSample[KnownSamples.Length];

        private TextAsset _snapshotJson;
        private GameObject _viewerGameObject;
        private SnapshotStats _stats = SnapshotStats.Empty;
        private bool _loadSucceeded;
        private string _statusText = "Select a DebugViewportSnapshotV1 JSON TextAsset.";
        private MessageType _statusType = MessageType.Info;
        private Vector2 _scrollPosition;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            DebugViewportSnapshotBrowserWindow window =
                GetWindow<DebugViewportSnapshotBrowserWindow>("Quantum Snapshot Browser");
            window.minSize = new Vector2(560f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(560f, 620f);
            RefreshSampleList();
            TryUseSelectionAsViewer();
        }

        private void OnSelectionChange()
        {
            TryUseSelectionAsViewer();
            Repaint();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            DrawSnapshotPicker();
            DrawStats();
            DrawViewerControls();
            DrawQuickLoadSamples();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Snapshot Browser", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_statusText, _statusType);
        }

        private void DrawSnapshotPicker()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Snapshot", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _snapshotJson = (TextAsset)EditorGUILayout.ObjectField(
                "JSON TextAsset",
                _snapshotJson,
                typeof(TextAsset),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSelectedSnapshot();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load / Refresh", GUILayout.Height(28f)))
            {
                RefreshSampleList();
                LoadSelectedSnapshot();
            }

            using (new EditorGUI.DisabledScope(_snapshotJson == null))
            {
                if (GUILayout.Button("Ping Asset", GUILayout.Height(28f)))
                {
                    EditorGUIUtility.PingObject(_snapshotJson);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Parsed Snapshot Stats", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!_loadSucceeded))
            {
                EditorGUILayout.LabelField("Centerline points", _stats.CenterlinePointCount.ToString());
                EditorGUILayout.LabelField("Frames", _stats.FrameCount.ToString());
                EditorGUILayout.LabelField("Lines", _stats.LineCount.ToString());
                EditorGUILayout.LabelField("Boxes", _stats.BoxCount.ToString());
                EditorGUILayout.LabelField("TrainPose", _stats.HasTrainPose ? "present" : "absent");
                EditorGUILayout.LabelField("TrainPose car count", _stats.TrainPoseCarCount.ToString());
            }
        }

        private void DrawViewerControls()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Scene Viewer", EditorStyles.boldLabel);

            _viewerGameObject = (GameObject)EditorGUILayout.ObjectField(
                "Viewer GameObject",
                _viewerGameObject,
                typeof(GameObject),
                true);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(_snapshotJson == null || !_loadSucceeded))
                {
                    if (GUILayout.Button("Create Viewer GameObject", GUILayout.Height(30f)))
                    {
                        CreateOrUpdateViewer();
                    }

                    if (GUILayout.Button("Rebuild Generated Boxes", GUILayout.Height(30f)))
                    {
                        RebuildGeneratedBoxes();
                    }
                }

                using (new EditorGUI.DisabledScope(_viewerGameObject == null))
                {
                    if (GUILayout.Button("Clear Generated Boxes", GUILayout.Height(30f)))
                    {
                        ClearGeneratedBoxes();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Scene viewer changes are disabled during Play Mode.", MessageType.Warning);
            }
        }

        private void DrawQuickLoadSamples()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Known Samples", EditorStyles.boldLabel);

            bool foundAny = false;
            for (int i = 0; i < _locatedSamples.Length; i++)
            {
                LocatedSnapshotSample located = _locatedSamples[i];
                if (located.Asset == null)
                {
                    continue;
                }

                foundAny = true;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(KnownSamples[i].Label, GUILayout.Width(150f)))
                {
                    _snapshotJson = located.Asset;
                    LoadSelectedSnapshot();
                }

                EditorGUILayout.SelectableLabel(located.AssetPath, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.EndHorizontal();
            }

            if (!foundAny)
            {
                EditorGUILayout.HelpBox("No known snapshot JSON TextAssets were found under Assets.", MessageType.Info);
            }
        }

        private void LoadSelectedSnapshot()
        {
            _stats = SnapshotStats.Empty;
            _loadSucceeded = false;

            if (!DebugViewportSnapshotV1JsonLoader.TryLoad(
                _snapshotJson,
                out DebugViewportSnapshotV1Dto snapshot,
                out string error))
            {
                _statusText = error;
                _statusType = MessageType.Warning;
                return;
            }

            _stats = SnapshotStats.From(snapshot);
            _loadSucceeded = true;
            _statusText = "Loaded " + _snapshotJson.name + ".";
            _statusType = MessageType.Info;
        }

        private void CreateOrUpdateViewer()
        {
            GameObject viewer = ResolveViewerGameObject(createIfMissing: true);
            if (viewer == null)
            {
                return;
            }

            ApplySnapshotToViewer(viewer);
            Selection.activeGameObject = viewer;
            _statusText = "Applied snapshot to " + viewer.name + ".";
            _statusType = MessageType.Info;
        }

        private void RebuildGeneratedBoxes()
        {
            GameObject viewer = ResolveViewerGameObject(createIfMissing: true);
            if (viewer == null)
            {
                return;
            }

            ViewerComponents components = ApplySnapshotToViewer(viewer);
            components.TransformVisualizer.Rebuild();
            Selection.activeGameObject = viewer;
            _statusText = "Rebuilt generated boxes on " + viewer.name + ".";
            _statusType = MessageType.Info;
        }

        private void ClearGeneratedBoxes()
        {
            if (_viewerGameObject == null)
            {
                _statusText = "No viewer GameObject is assigned.";
                _statusType = MessageType.Warning;
                return;
            }

            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                _viewerGameObject.GetComponent<DebugViewportSnapshotV1TransformVisualizer>();
            if (transformVisualizer == null)
            {
                _statusText = _viewerGameObject.name + " does not have a DebugViewportSnapshotV1TransformVisualizer.";
                _statusType = MessageType.Warning;
                return;
            }

            Undo.RecordObject(transformVisualizer, "Clear Quantum Snapshot Boxes");
            transformVisualizer.Clear();
            MarkSceneObjectDirty(_viewerGameObject);
            _statusText = "Cleared generated boxes on " + _viewerGameObject.name + ".";
            _statusType = MessageType.Info;
        }

        private ViewerComponents ApplySnapshotToViewer(GameObject viewer)
        {
            ViewerComponents components = EnsureViewerComponents(viewer);

            Undo.RecordObject(components.GizmoVisualizer, "Apply Quantum Snapshot");
            components.GizmoVisualizer.ApplySnapshot(_snapshotJson);
            EditorUtility.SetDirty(components.GizmoVisualizer);

            Undo.RecordObject(components.TransformVisualizer, "Apply Quantum Snapshot");
            components.TransformVisualizer.ApplySnapshot(_snapshotJson);
            EditorUtility.SetDirty(components.TransformVisualizer);

            MarkSceneObjectDirty(viewer);
            return components;
        }

        private GameObject ResolveViewerGameObject(bool createIfMissing)
        {
            if (_viewerGameObject != null)
            {
                EnsureViewerComponents(_viewerGameObject);
                return _viewerGameObject;
            }

            GameObject selectedViewer = FindViewerOnSelection();
            if (selectedViewer != null)
            {
                _viewerGameObject = selectedViewer;
                EnsureViewerComponents(_viewerGameObject);
                return _viewerGameObject;
            }

            GameObject existing = GameObject.Find(ViewerObjectName);
            if (existing != null)
            {
                _viewerGameObject = existing;
                EnsureViewerComponents(_viewerGameObject);
                return _viewerGameObject;
            }

            if (!createIfMissing)
            {
                return null;
            }

            var created = new GameObject(ViewerObjectName);
            Undo.RegisterCreatedObjectUndo(created, "Create Quantum Snapshot Viewer");
            _viewerGameObject = created;
            EnsureViewerComponents(created);
            MarkSceneObjectDirty(created);
            return created;
        }

        private ViewerComponents EnsureViewerComponents(GameObject viewer)
        {
            DebugViewportSnapshotV1GizmoVisualizer gizmoVisualizer =
                viewer.GetComponent<DebugViewportSnapshotV1GizmoVisualizer>();
            if (gizmoVisualizer == null)
            {
                gizmoVisualizer = Undo.AddComponent<DebugViewportSnapshotV1GizmoVisualizer>(viewer);
            }

            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                viewer.GetComponent<DebugViewportSnapshotV1TransformVisualizer>();
            if (transformVisualizer == null)
            {
                transformVisualizer = Undo.AddComponent<DebugViewportSnapshotV1TransformVisualizer>(viewer);
            }

            return new ViewerComponents(gizmoVisualizer, transformVisualizer);
        }

        private void RefreshSampleList()
        {
            for (int i = 0; i < _locatedSamples.Length; i++)
            {
                _locatedSamples[i] = LocatedSnapshotSample.Empty;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileName(path);

                for (int sampleIndex = 0; sampleIndex < KnownSamples.Length; sampleIndex++)
                {
                    if (_locatedSamples[sampleIndex].Asset != null ||
                        !string.Equals(fileName, KnownSamples[sampleIndex].FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null)
                    {
                        _locatedSamples[sampleIndex] = new LocatedSnapshotSample(asset, path);
                    }
                }
            }
        }

        private void TryUseSelectionAsViewer()
        {
            if (_viewerGameObject != null)
            {
                return;
            }

            _viewerGameObject = FindViewerOnSelection();
        }

        private static GameObject FindViewerOnSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return null;
            }

            if (selected.GetComponent<DebugViewportSnapshotV1GizmoVisualizer>() != null ||
                selected.GetComponent<DebugViewportSnapshotV1TransformVisualizer>() != null)
            {
                return selected;
            }

            return null;
        }

        private static void MarkSceneObjectDirty(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            EditorUtility.SetDirty(gameObject);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        private readonly struct ViewerComponents
        {
            public ViewerComponents(
                DebugViewportSnapshotV1GizmoVisualizer gizmoVisualizer,
                DebugViewportSnapshotV1TransformVisualizer transformVisualizer)
            {
                GizmoVisualizer = gizmoVisualizer;
                TransformVisualizer = transformVisualizer;
            }

            public DebugViewportSnapshotV1GizmoVisualizer GizmoVisualizer { get; }

            public DebugViewportSnapshotV1TransformVisualizer TransformVisualizer { get; }
        }

        private readonly struct KnownSnapshotSample
        {
            public KnownSnapshotSample(string label, string fileName)
            {
                Label = label;
                FileName = fileName;
            }

            public string Label { get; }

            public string FileName { get; }
        }

        private readonly struct LocatedSnapshotSample
        {
            public static readonly LocatedSnapshotSample Empty = new LocatedSnapshotSample(null, string.Empty);

            public LocatedSnapshotSample(TextAsset asset, string assetPath)
            {
                Asset = asset;
                AssetPath = assetPath;
            }

            public TextAsset Asset { get; }

            public string AssetPath { get; }
        }

        private readonly struct SnapshotStats
        {
            public static readonly SnapshotStats Empty = new SnapshotStats(0, 0, 0, 0, false, 0);

            private SnapshotStats(
                int centerlinePointCount,
                int frameCount,
                int lineCount,
                int boxCount,
                bool hasTrainPose,
                int trainPoseCarCount)
            {
                CenterlinePointCount = centerlinePointCount;
                FrameCount = frameCount;
                LineCount = lineCount;
                BoxCount = boxCount;
                HasTrainPose = hasTrainPose;
                TrainPoseCarCount = trainPoseCarCount;
            }

            public int CenterlinePointCount { get; }

            public int FrameCount { get; }

            public int LineCount { get; }

            public int BoxCount { get; }

            public bool HasTrainPose { get; }

            public int TrainPoseCarCount { get; }

            public static SnapshotStats From(DebugViewportSnapshotV1Dto snapshot)
            {
                bool hasTrainPose = snapshot != null && snapshot.trainPose != null;
                return new SnapshotStats(
                    snapshot != null && snapshot.centerlinePoints != null ? snapshot.centerlinePoints.Length : 0,
                    snapshot != null && snapshot.frames != null ? snapshot.frames.Length : 0,
                    snapshot != null && snapshot.lines != null ? snapshot.lines.Length : 0,
                    snapshot != null && snapshot.boxes != null ? snapshot.boxes.Length : 0,
                    hasTrainPose,
                    hasTrainPose ? ResolveTrainPoseCarCount(snapshot.trainPose) : 0);
            }

            private static int ResolveTrainPoseCarCount(TrainPoseExportV1Dto trainPose)
            {
                if (trainPose == null)
                {
                    return 0;
                }

                if (trainPose.cars != null)
                {
                    return trainPose.cars.Length;
                }

                return trainPose.definition != null ? trainPose.definition.carCount : 0;
            }
        }
    }
}

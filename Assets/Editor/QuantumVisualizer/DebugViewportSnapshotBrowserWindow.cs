using System;
using System.Collections.Generic;
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
        private const string DebugDataFolderAssetPath = "Assets/DebugData";
        private const string GalleryHtmlAssetPath = DebugDataFolderAssetPath + "/index.html";
        private const string BrowserHtmlAssetPath = DebugDataFolderAssetPath + "/browser.html";

        private static readonly KnownSnapshotSample[] KnownSamples =
        {
            new KnownSnapshotSample("Built-in", "DebugViewportSnapshotV1.sample.json"),
            new KnownSnapshotSample("BankingProfile", "DebugViewportSnapshotV1.banking-profile.sample.json"),
            new KnownSnapshotSample("Straight Line", "Milestone7.synthetic.straight_line.snapshot.json"),
            new KnownSnapshotSample("Simple Hill", "Milestone7.synthetic.simple_hill.snapshot.json"),
            new KnownSnapshotSample("Banked Turn", "Milestone7.synthetic.banked_turn.snapshot.json"),
            new KnownSnapshotSample("Desc/Asc Curve", "Milestone7.synthetic.descending_ascending_curve.snapshot.json")
        };

        private readonly List<SnapshotListItem> _snapshotListItems = new List<SnapshotListItem>();

        private TextAsset _snapshotJson;
        private GameObject _viewerGameObject;
        private SnapshotStats _stats = SnapshotStats.Empty;
        private bool _loadSucceeded;
        private string _statusText = "Select a DebugViewportSnapshotV1 JSON TextAsset.";
        private MessageType _statusType = MessageType.Info;
        private Vector2 _scrollPosition;
        private string _selectedSnapshotAssetPath;
        private string _lastObservedSnapshotText;
        private bool _hasObservedSnapshotText;
        private bool _hasKnownSyncedArtifacts;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            DebugViewportSnapshotBrowserWindow window =
                GetWindow<DebugViewportSnapshotBrowserWindow>("Quantum Snapshot Browser");
            window.minSize = new Vector2(680f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(680f, 720f);
            RefreshSnapshotList();
            TryUseSelectionAsViewer();
            TryUseExistingViewer();
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnInspectorUpdate()
        {
            if (ReloadSelectedSnapshotIfTextChanged())
            {
                Repaint();
            }
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
            DrawStatusPanel();
            DrawSnapshotPicker();
            DrawArtifactButtons();
            DrawSnapshotList();
            DrawStats();
            DrawViewerControls();

            EditorGUILayout.EndScrollView();
        }

        private void OnProjectChanged()
        {
            RefreshSnapshotList();
            bool selectedSnapshotWasRemoved = RefreshSelectedSnapshotReference();

            if (_snapshotJson != null)
            {
                LoadSelectedSnapshot();
            }
            else if (!selectedSnapshotWasRemoved)
            {
                _statusText = "Asset list refreshed.";
                _statusType = MessageType.Info;
            }

            Repaint();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Snapshot Browser", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_statusText, _statusType);
        }

        private void DrawStatusPanel()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            bool wroteStatus = false;

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Scene-editing actions are disabled in Play Mode.", MessageType.Warning);
                wroteStatus = true;
            }

            if (_snapshotJson == null)
            {
                EditorGUILayout.HelpBox("No snapshot selected.", MessageType.Warning);
                wroteStatus = true;
            }
            else if (!_loadSucceeded)
            {
                EditorGUILayout.HelpBox("Current snapshot is not loadable: " + _statusText, MessageType.Warning);
                wroteStatus = true;
            }

            if (!_hasKnownSyncedArtifacts)
            {
                EditorGUILayout.HelpBox(
                    "No known synced DebugViewportSnapshotV1 artifacts were found under Assets/DebugData.",
                    MessageType.Info);
                wroteStatus = true;
            }

            if (_viewerGameObject == null && FindViewerInOpenScenes() == null)
            {
                EditorGUILayout.HelpBox(
                    "No Quantum Snapshot Viewer is assigned or found in the open scene.",
                    MessageType.Info);
                wroteStatus = true;
            }

            if (!wroteStatus)
            {
                EditorGUILayout.HelpBox("Ready.", MessageType.Info);
            }
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
            if (GUILayout.Button("Refresh List", GUILayout.Height(28f)))
            {
                RefreshSnapshotList();
                _statusText = "Snapshot list refreshed.";
                _statusType = MessageType.Info;
            }

            using (new EditorGUI.DisabledScope(_snapshotJson == null))
            {
                if (GUILayout.Button("Load Selected", GUILayout.Height(28f)))
                {
                    LoadSelectedSnapshot();
                }

                if (GUILayout.Button("Ping Selected", GUILayout.Height(28f)))
                {
                    EditorGUIUtility.PingObject(_snapshotJson);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawArtifactButtons()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("DebugData Artifacts", EditorStyles.boldLabel);

            bool hasGallery = AssetFileExists(GalleryHtmlAssetPath);
            bool hasBrowser = AssetFileExists(BrowserHtmlAssetPath);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasGallery))
            {
                if (GUILayout.Button("Open Gallery index.html", GUILayout.Height(28f)))
                {
                    OpenAssetFile(GalleryHtmlAssetPath);
                }
            }

            using (new EditorGUI.DisabledScope(!hasBrowser))
            {
                if (GUILayout.Button("Open Browser browser.html", GUILayout.Height(28f)))
                {
                    OpenAssetFile(BrowserHtmlAssetPath);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!hasGallery && !hasBrowser)
            {
                EditorGUILayout.HelpBox(
                    "No generated HTML gallery or browser was found under Assets/DebugData.",
                    MessageType.Info);
            }
        }

        private void DrawSnapshotList()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Discovered Snapshots", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(_snapshotListItems.Count.ToString(), GUILayout.Width(42f));
            EditorGUILayout.EndHorizontal();

            if (_snapshotListItems.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No known generated snapshot files or other valid DebugViewportSnapshotV1 JSON TextAssets were found under Assets.",
                    MessageType.Info);
                return;
            }

            for (int i = 0; i < _snapshotListItems.Count; i++)
            {
                DrawSnapshotListRow(_snapshotListItems[i]);
            }
        }

        private void DrawSnapshotListRow(SnapshotListItem item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            string title = item.IsKnown
                ? item.KnownLabel + " - " + item.Asset.name
                : item.Asset.name;

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(item.Asset == null))
            {
                if (GUILayout.Button("Load", GUILayout.Width(58f)))
                {
                    LoadSnapshotListItem(item);
                }
            }

            using (new EditorGUI.DisabledScope(item.Asset == null || !item.IsValid || Application.isPlaying))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(58f)))
                {
                    LoadSnapshotListItem(item);
                    if (_loadSucceeded)
                    {
                        CreateOrUpdateViewer();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(item.Asset == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(52f)))
                {
                    EditorGUIUtility.PingObject(item.Asset);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.SelectableLabel(
                item.AssetPath,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (!item.IsValid)
            {
                EditorGUILayout.HelpBox(item.Error, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
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

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_loadSucceeded))
            {
                EditorGUILayout.LabelField("metadata.units", DisplayText(_stats.Units));
                EditorGUILayout.LabelField("metadata.sourceFixtureName", DisplayText(_stats.SourceFixtureName));
                EditorGUILayout.LabelField("metadata.sampleCount", _stats.MetadataSampleCount.ToString());
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Role Counts", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!_loadSucceeded))
            {
                EditorGUILayout.LabelField(DebugViewportSnapshotV1Vocabulary.TrainBodyRole, _stats.TrainBodyCount.ToString());
                EditorGUILayout.LabelField(DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole, _stats.TrainBodyBankingProfileCount.ToString());
                EditorGUILayout.LabelField(DebugViewportSnapshotV1Vocabulary.TrainBogieRole, _stats.TrainBogieCount.ToString());
                EditorGUILayout.LabelField(DebugViewportSnapshotV1Vocabulary.TrainWheelRole, _stats.TrainWheelCount.ToString());
                EditorGUILayout.LabelField("unknown", _stats.UnknownRoleCount.ToString());
            }
        }

        private void DrawViewerControls()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Scene Viewer", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _viewerGameObject = (GameObject)EditorGUILayout.ObjectField(
                "Viewer GameObject",
                _viewerGameObject,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck() && _viewerGameObject != null && !IsSceneObject(_viewerGameObject))
            {
                _statusText = "Viewer must be a scene GameObject, not a prefab or asset.";
                _statusType = MessageType.Warning;
                _viewerGameObject = null;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find Viewer", GUILayout.Height(28f)))
            {
                FindAndAssignViewer();
            }

            using (new EditorGUI.DisabledScope(_viewerGameObject == null && FindViewerInOpenScenes() == null))
            {
                if (GUILayout.Button("Select Viewer", GUILayout.Height(28f)))
                {
                    SelectViewer();
                }
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(_snapshotJson == null || !_loadSucceeded))
                {
                    if (GUILayout.Button("Create / Update Viewer", GUILayout.Height(30f)))
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
        }

        private bool LoadSelectedSnapshot()
        {
            _stats = SnapshotStats.Empty;
            _loadSucceeded = false;
            TrackObservedSnapshotText();

            if (!DebugViewportSnapshotV1JsonLoader.TryLoad(
                _snapshotJson,
                out DebugViewportSnapshotV1Dto snapshot,
                out string error))
            {
                _statusText = error;
                _statusType = MessageType.Warning;
                return false;
            }

            _stats = SnapshotStats.From(snapshot);
            _loadSucceeded = true;

            string assetPath = AssetDatabase.GetAssetPath(_snapshotJson);
            _statusText = string.IsNullOrEmpty(assetPath)
                ? "Loaded " + _snapshotJson.name + "."
                : "Loaded " + _snapshotJson.name + " from " + assetPath + ".";
            _statusType = MessageType.Info;
            return true;
        }

        private void LoadSnapshotListItem(SnapshotListItem item)
        {
            _snapshotJson = item.Asset;
            LoadSelectedSnapshot();
        }

        private void CreateOrUpdateViewer()
        {
            if (Application.isPlaying)
            {
                _statusText = "Scene-editing actions are disabled in Play Mode.";
                _statusType = MessageType.Warning;
                return;
            }

            if (!EnsureSelectedSnapshotIsLoaded())
            {
                return;
            }

            GameObject viewer = ResolveViewerGameObject(createIfMissing: true);
            if (viewer == null)
            {
                _statusText = "No viewer assigned or found.";
                _statusType = MessageType.Warning;
                return;
            }

            ApplySnapshotToViewer(viewer);
            Selection.activeGameObject = viewer;
            _statusText = "Applied snapshot to " + viewer.name + ".";
            _statusType = MessageType.Info;
        }

        private void RebuildGeneratedBoxes()
        {
            if (Application.isPlaying)
            {
                _statusText = "Scene-editing actions are disabled in Play Mode.";
                _statusType = MessageType.Warning;
                return;
            }

            if (!EnsureSelectedSnapshotIsLoaded())
            {
                return;
            }

            GameObject viewer = ResolveViewerGameObject(createIfMissing: true);
            if (viewer == null)
            {
                _statusText = "No viewer assigned or found.";
                _statusType = MessageType.Warning;
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
            if (Application.isPlaying)
            {
                _statusText = "Scene-editing actions are disabled in Play Mode.";
                _statusType = MessageType.Warning;
                return;
            }

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
            GameObject assignedViewer = GetAssignedViewer();
            if (assignedViewer != null)
            {
                _viewerGameObject = assignedViewer;
                if (createIfMissing)
                {
                    EnsureViewerComponents(_viewerGameObject);
                }

                return _viewerGameObject;
            }

            GameObject selectedViewer = FindViewerOnSelection();
            if (selectedViewer != null)
            {
                _viewerGameObject = selectedViewer;
                if (createIfMissing)
                {
                    EnsureViewerComponents(_viewerGameObject);
                }

                return _viewerGameObject;
            }

            GameObject existing = FindViewerInOpenScenes();
            if (existing != null)
            {
                _viewerGameObject = existing;
                if (createIfMissing)
                {
                    EnsureViewerComponents(_viewerGameObject);
                }

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

        private void RefreshSnapshotList()
        {
            _snapshotListItems.Clear();
            _hasKnownSyncedArtifacts = false;

            var jsonAssetPaths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonAssetPaths.Add(path);
                }
            }

            jsonAssetPaths.Sort(StringComparer.OrdinalIgnoreCase);

            var includedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int sampleIndex = 0; sampleIndex < KnownSamples.Length; sampleIndex++)
            {
                KnownSnapshotSample knownSample = KnownSamples[sampleIndex];
                for (int pathIndex = 0; pathIndex < jsonAssetPaths.Count; pathIndex++)
                {
                    string path = jsonAssetPaths[pathIndex];
                    string fileName = Path.GetFileName(path);
                    if (!string.Equals(fileName, knownSample.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset == null)
                    {
                        continue;
                    }

                    SnapshotValidationResult validation = ValidateSnapshot(asset);
                    _snapshotListItems.Add(new SnapshotListItem(
                        asset,
                        path,
                        knownSample.Label,
                        true,
                        validation.IsValid,
                        validation.Error));
                    includedPaths.Add(path);

                    if (IsDebugDataAssetPath(path))
                    {
                        _hasKnownSyncedArtifacts = true;
                    }
                }
            }

            for (int i = 0; i < jsonAssetPaths.Count; i++)
            {
                string path = jsonAssetPaths[i];
                if (includedPaths.Contains(path))
                {
                    continue;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                SnapshotValidationResult validation = ValidateSnapshot(asset);
                if (!validation.IsValid)
                {
                    continue;
                }

                _snapshotListItems.Add(new SnapshotListItem(
                    asset,
                    path,
                    string.Empty,
                    false,
                    true,
                    string.Empty));
            }
        }

        private void TryUseSelectionAsViewer()
        {
            GameObject selectedViewer = FindViewerOnSelection();
            if (selectedViewer != null)
            {
                _viewerGameObject = selectedViewer;
            }
        }

        private void TryUseExistingViewer()
        {
            if (_viewerGameObject != null)
            {
                return;
            }

            _viewerGameObject = FindViewerInOpenScenes();
        }

        private void FindAndAssignViewer()
        {
            GameObject viewer = ResolveViewerGameObject(createIfMissing: false);
            if (viewer == null)
            {
                _statusText = "No Quantum Snapshot Viewer was found in the open scene.";
                _statusType = MessageType.Warning;
                return;
            }

            _viewerGameObject = viewer;
            _statusText = "Assigned " + viewer.name + ".";
            _statusType = MessageType.Info;
        }

        private void SelectViewer()
        {
            GameObject viewer = ResolveViewerGameObject(createIfMissing: false);
            if (viewer == null)
            {
                _statusText = "No Quantum Snapshot Viewer is assigned or found.";
                _statusType = MessageType.Warning;
                return;
            }

            _viewerGameObject = viewer;
            Selection.activeGameObject = viewer;
            EditorGUIUtility.PingObject(viewer);
            _statusText = "Selected " + viewer.name + ".";
            _statusType = MessageType.Info;
        }

        private bool EnsureSelectedSnapshotIsLoaded()
        {
            if (_snapshotJson == null)
            {
                _statusText = "No snapshot selected.";
                _statusType = MessageType.Warning;
                return false;
            }

            return _loadSucceeded || LoadSelectedSnapshot();
        }

        private bool ReloadSelectedSnapshotIfTextChanged()
        {
            if (_snapshotJson == null)
            {
                _hasObservedSnapshotText = false;
                _lastObservedSnapshotText = null;
                _selectedSnapshotAssetPath = null;
                return false;
            }

            string currentText = _snapshotJson.text;
            if (!_hasObservedSnapshotText)
            {
                TrackObservedSnapshotText();
                return false;
            }

            if (string.Equals(currentText, _lastObservedSnapshotText, StringComparison.Ordinal))
            {
                return false;
            }

            LoadSelectedSnapshot();
            if (_loadSucceeded)
            {
                _statusText = "Reloaded changed snapshot " + _snapshotJson.name + ".";
                _statusType = MessageType.Info;
            }

            return true;
        }

        private bool RefreshSelectedSnapshotReference()
        {
            if (string.IsNullOrEmpty(_selectedSnapshotAssetPath))
            {
                return false;
            }

            TextAsset reloadedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(_selectedSnapshotAssetPath);
            if (reloadedAsset != null)
            {
                _snapshotJson = reloadedAsset;
                return false;
            }

            _snapshotJson = null;
            _stats = SnapshotStats.Empty;
            _loadSucceeded = false;
            _hasObservedSnapshotText = false;
            _lastObservedSnapshotText = null;
            _statusText = "Selected snapshot asset was removed: " + _selectedSnapshotAssetPath + ".";
            _statusType = MessageType.Warning;
            return true;
        }

        private void TrackObservedSnapshotText()
        {
            _selectedSnapshotAssetPath = _snapshotJson != null
                ? AssetDatabase.GetAssetPath(_snapshotJson)
                : null;
            _lastObservedSnapshotText = _snapshotJson != null ? _snapshotJson.text : null;
            _hasObservedSnapshotText = _snapshotJson != null;
        }

        private static SnapshotValidationResult ValidateSnapshot(TextAsset asset)
        {
            if (DebugViewportSnapshotV1JsonLoader.TryLoad(
                asset,
                out DebugViewportSnapshotV1Dto _,
                out string error))
            {
                return SnapshotValidationResult.Valid;
            }

            return new SnapshotValidationResult(false, error);
        }

        private GameObject GetAssignedViewer()
        {
            if (_viewerGameObject == null)
            {
                return null;
            }

            if (IsSceneObject(_viewerGameObject))
            {
                return _viewerGameObject;
            }

            _viewerGameObject = null;
            return null;
        }

        private static GameObject FindViewerOnSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || !IsSceneObject(selected))
            {
                return null;
            }

            if (IsViewerCandidate(selected))
            {
                return selected;
            }

            DebugViewportSnapshotV1GizmoVisualizer gizmoVisualizer =
                selected.GetComponentInParent<DebugViewportSnapshotV1GizmoVisualizer>();
            if (gizmoVisualizer != null && IsSceneObject(gizmoVisualizer.gameObject))
            {
                return gizmoVisualizer.gameObject;
            }

            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                selected.GetComponentInParent<DebugViewportSnapshotV1TransformVisualizer>();
            if (transformVisualizer != null && IsSceneObject(transformVisualizer.gameObject))
            {
                return transformVisualizer.gameObject;
            }

            return null;
        }

        private static GameObject FindViewerInOpenScenes()
        {
            GameObject namedViewer = null;
            GameObject componentViewer = null;
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

            for (int i = 0; i < objects.Length; i++)
            {
                GameObject gameObject = objects[i];
                if (!IsSceneObject(gameObject))
                {
                    continue;
                }

                if (string.Equals(gameObject.name, ViewerObjectName, StringComparison.Ordinal))
                {
                    if (IsViewerCandidate(gameObject))
                    {
                        return gameObject;
                    }

                    if (namedViewer == null)
                    {
                        namedViewer = gameObject;
                    }
                }

                if (componentViewer == null && HasViewerComponent(gameObject))
                {
                    componentViewer = gameObject;
                }
            }

            return namedViewer != null ? namedViewer : componentViewer;
        }

        private static bool IsViewerCandidate(GameObject gameObject)
        {
            return string.Equals(gameObject.name, ViewerObjectName, StringComparison.Ordinal) ||
                HasViewerComponent(gameObject);
        }

        private static bool HasViewerComponent(GameObject gameObject)
        {
            return gameObject.GetComponent<DebugViewportSnapshotV1GizmoVisualizer>() != null ||
                gameObject.GetComponent<DebugViewportSnapshotV1TransformVisualizer>() != null;
        }

        private static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && !EditorUtility.IsPersistent(gameObject);
        }

        private static bool IsDebugDataAssetPath(string assetPath)
        {
            return assetPath.StartsWith(DebugDataFolderAssetPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool AssetFileExists(string assetPath)
        {
            string fullPath = AssetPathToFullPath(assetPath);
            return File.Exists(fullPath);
        }

        private void OpenAssetFile(string assetPath)
        {
            string fullPath = AssetPathToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                _statusText = "Artifact file was not found: " + assetPath + ".";
                _statusType = MessageType.Warning;
                return;
            }

            Application.OpenURL(new Uri(fullPath).AbsoluteUri);
            _statusText = "Opened " + assetPath + ".";
            _statusType = MessageType.Info;
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string normalizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, normalizedAssetPath));
        }

        private static string DisplayText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
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

        private readonly struct SnapshotListItem
        {
            public SnapshotListItem(
                TextAsset asset,
                string assetPath,
                string knownLabel,
                bool isKnown,
                bool isValid,
                string error)
            {
                Asset = asset;
                AssetPath = assetPath;
                KnownLabel = knownLabel;
                IsKnown = isKnown;
                IsValid = isValid;
                Error = error;
            }

            public TextAsset Asset { get; }

            public string AssetPath { get; }

            public string KnownLabel { get; }

            public bool IsKnown { get; }

            public bool IsValid { get; }

            public string Error { get; }
        }

        private readonly struct SnapshotValidationResult
        {
            public static readonly SnapshotValidationResult Valid = new SnapshotValidationResult(true, string.Empty);

            public SnapshotValidationResult(bool isValid, string error)
            {
                IsValid = isValid;
                Error = error;
            }

            public bool IsValid { get; }

            public string Error { get; }
        }

        private readonly struct SnapshotStats
        {
            public static readonly SnapshotStats Empty = new SnapshotStats(
                0,
                0,
                0,
                0,
                false,
                0,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                0);

            private SnapshotStats(
                int centerlinePointCount,
                int frameCount,
                int lineCount,
                int boxCount,
                bool hasTrainPose,
                int trainPoseCarCount,
                string units,
                string sourceFixtureName,
                int metadataSampleCount,
                int trainBodyCount,
                int trainBodyBankingProfileCount,
                int trainBogieCount,
                int trainWheelCount,
                int unknownRoleCount)
            {
                CenterlinePointCount = centerlinePointCount;
                FrameCount = frameCount;
                LineCount = lineCount;
                BoxCount = boxCount;
                HasTrainPose = hasTrainPose;
                TrainPoseCarCount = trainPoseCarCount;
                Units = units;
                SourceFixtureName = sourceFixtureName;
                MetadataSampleCount = metadataSampleCount;
                TrainBodyCount = trainBodyCount;
                TrainBodyBankingProfileCount = trainBodyBankingProfileCount;
                TrainBogieCount = trainBogieCount;
                TrainWheelCount = trainWheelCount;
                UnknownRoleCount = unknownRoleCount;
            }

            public int CenterlinePointCount { get; }

            public int FrameCount { get; }

            public int LineCount { get; }

            public int BoxCount { get; }

            public bool HasTrainPose { get; }

            public int TrainPoseCarCount { get; }

            public string Units { get; }

            public string SourceFixtureName { get; }

            public int MetadataSampleCount { get; }

            public int TrainBodyCount { get; }

            public int TrainBodyBankingProfileCount { get; }

            public int TrainBogieCount { get; }

            public int TrainWheelCount { get; }

            public int UnknownRoleCount { get; }

            public static SnapshotStats From(DebugViewportSnapshotV1Dto snapshot)
            {
                bool hasTrainPose = snapshot != null && snapshot.trainPose != null;
                RoleCounts roleCounts = RoleCounts.From(snapshot);
                DebugViewportMetadataV1Dto metadata = snapshot != null ? snapshot.metadata : null;

                return new SnapshotStats(
                    snapshot != null && snapshot.centerlinePoints != null ? snapshot.centerlinePoints.Length : 0,
                    snapshot != null && snapshot.frames != null ? snapshot.frames.Length : 0,
                    snapshot != null && snapshot.lines != null ? snapshot.lines.Length : 0,
                    snapshot != null && snapshot.boxes != null ? snapshot.boxes.Length : 0,
                    hasTrainPose,
                    hasTrainPose ? ResolveTrainPoseCarCount(snapshot.trainPose) : 0,
                    metadata != null ? metadata.units : string.Empty,
                    metadata != null ? metadata.sourceFixtureName : string.Empty,
                    metadata != null ? metadata.sampleCount : 0,
                    roleCounts.TrainBody,
                    roleCounts.TrainBodyBankingProfile,
                    roleCounts.TrainBogie,
                    roleCounts.TrainWheel,
                    roleCounts.Unknown);
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

        private readonly struct RoleCounts
        {
            private RoleCounts(
                int trainBody,
                int trainBodyBankingProfile,
                int trainBogie,
                int trainWheel,
                int unknown)
            {
                TrainBody = trainBody;
                TrainBodyBankingProfile = trainBodyBankingProfile;
                TrainBogie = trainBogie;
                TrainWheel = trainWheel;
                Unknown = unknown;
            }

            public int TrainBody { get; }

            public int TrainBodyBankingProfile { get; }

            public int TrainBogie { get; }

            public int TrainWheel { get; }

            public int Unknown { get; }

            public static RoleCounts From(DebugViewportSnapshotV1Dto snapshot)
            {
                int trainBody = 0;
                int trainBodyBankingProfile = 0;
                int trainBogie = 0;
                int trainWheel = 0;
                int unknown = 0;

                if (snapshot == null || snapshot.boxes == null)
                {
                    return new RoleCounts(0, 0, 0, 0, 0);
                }

                for (int i = 0; i < snapshot.boxes.Length; i++)
                {
                    DebugViewportBoxV1Dto box = snapshot.boxes[i];
                    string role = box != null ? box.role : null;

                    if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyRole, StringComparison.Ordinal))
                    {
                        trainBody++;
                    }
                    else if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole, StringComparison.Ordinal))
                    {
                        trainBodyBankingProfile++;
                    }
                    else if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBogieRole, StringComparison.Ordinal))
                    {
                        trainBogie++;
                    }
                    else if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainWheelRole, StringComparison.Ordinal))
                    {
                        trainWheel++;
                    }
                    else
                    {
                        unknown++;
                    }
                }

                return new RoleCounts(
                    trainBody,
                    trainBodyBankingProfile,
                    trainBogie,
                    trainWheel,
                    unknown);
            }
        }
    }
}

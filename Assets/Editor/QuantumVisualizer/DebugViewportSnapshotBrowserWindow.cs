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
        private const string GeneratedArtifactDefaultSourcePath = "artifacts/debug-viewport";
        private const string ArtifactSourceEditorPrefsKey =
            "QuantumVisualizer.DebugViewportSnapshotBrowserWindow.ArtifactSourceFolder";

        private static readonly KnownSnapshotSample[] KnownSamples =
        {
            new KnownSnapshotSample(
                "Built-in",
                "DebugViewportSnapshotV1.sample.json",
                SnapshotSourceGroup.BuiltIn),
            new KnownSnapshotSample(
                "BankingProfile",
                "DebugViewportSnapshotV1.banking-profile.sample.json",
                SnapshotSourceGroup.BankingProfile),
            new KnownSnapshotSample(
                "Straight Line",
                "Milestone7.synthetic.straight_line.snapshot.json",
                SnapshotSourceGroup.CsvFixtures),
            new KnownSnapshotSample(
                "Simple Hill",
                "Milestone7.synthetic.simple_hill.snapshot.json",
                SnapshotSourceGroup.CsvFixtures),
            new KnownSnapshotSample(
                "Banked Turn",
                "Milestone7.synthetic.banked_turn.snapshot.json",
                SnapshotSourceGroup.CsvFixtures),
            new KnownSnapshotSample(
                "Desc/Asc Curve",
                "Milestone7.synthetic.descending_ascending_curve.snapshot.json",
                SnapshotSourceGroup.CsvFixtures)
        };

        private static readonly SnapshotSourceGroup[] SnapshotGroupOrder =
        {
            SnapshotSourceGroup.BuiltIn,
            SnapshotSourceGroup.BankingProfile,
            SnapshotSourceGroup.CsvFixtures,
            SnapshotSourceGroup.OtherValidSnapshots,
            SnapshotSourceGroup.InvalidDebugDataJson
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
        private bool _hasDebugDataArtifacts;
        private string _artifactSourceFolder;
        private bool _cleanBeforeImport;
        private string _lastArtifactImportText = "No generated artifacts imported in this window session.";
        private MessageType _lastArtifactImportType = MessageType.Info;

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
            InitializeArtifactSourceFolder();
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
            DrawGeneratedArtifactWorkflow();
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

            if (!_hasDebugDataArtifacts)
            {
                EditorGUILayout.HelpBox(
                    "No generated JSON, SVG, or HTML artifacts were found under Assets/DebugData.",
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

        private void DrawGeneratedArtifactWorkflow()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Generated Artifacts", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Copy backend-generated JSON, SVG, and HTML into Assets/DebugData.");
            EditorGUILayout.LabelField("Target", DebugDataFolderAssetPath);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _artifactSourceFolder = EditorGUILayout.TextField("Source Folder", _artifactSourceFolder);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(ArtifactSourceEditorPrefsKey, _artifactSourceFolder ?? string.Empty);
            }

            if (GUILayout.Button("Browse", GUILayout.Width(72f)))
            {
                BrowseArtifactSourceFolder();
            }

            if (GUILayout.Button("Default", GUILayout.Width(72f)))
            {
                _artifactSourceFolder = GeneratedArtifactDefaultSourcePath;
                EditorPrefs.SetString(ArtifactSourceEditorPrefsKey, _artifactSourceFolder);
            }

            EditorGUILayout.EndHorizontal();

            _cleanBeforeImport = EditorGUILayout.ToggleLeft(
                "Clean Assets/DebugData JSON, SVG, and HTML before import",
                _cleanBeforeImport);

            if (GUILayout.Button("Import / Refresh Generated Artifacts", GUILayout.Height(30f)))
            {
                ImportGeneratedArtifacts();
            }

            EditorGUILayout.HelpBox(_lastArtifactImportText, _lastArtifactImportType);
            EditorGUILayout.EndVertical();
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
                    "No generated snapshot files, valid DebugViewportSnapshotV1 JSON TextAssets, or invalid DebugData JSON warnings were found under Assets.",
                    MessageType.Info);
                return;
            }

            for (int groupIndex = 0; groupIndex < SnapshotGroupOrder.Length; groupIndex++)
            {
                SnapshotSourceGroup group = SnapshotGroupOrder[groupIndex];
                int groupCount = CountSnapshotListItems(group);
                if (groupCount == 0)
                {
                    continue;
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(GetGroupLabel(group) + " (" + groupCount + ")", EditorStyles.boldLabel);

                for (int i = 0; i < _snapshotListItems.Count; i++)
                {
                    SnapshotListItem item = _snapshotListItems[i];
                    if (item.Group == group)
                    {
                        DrawSnapshotListRow(item);
                    }
                }
            }
        }

        private void DrawSnapshotListRow(SnapshotListItem item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            string assetName = item.Asset != null ? item.Asset.name : Path.GetFileNameWithoutExtension(item.AssetPath);
            string title = item.IsKnown
                ? item.KnownLabel + " - " + assetName
                : assetName;

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
            else
            {
                DrawSnapshotListRowStats(item.Stats);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawSnapshotListRowStats(SnapshotStats stats)
        {
            EditorGUILayout.LabelField(
                "metadata.sourceFixtureName",
                DisplayText(stats.SourceFixtureName));
            EditorGUILayout.LabelField(
                "metadata.sampleCount",
                stats.MetadataSampleCount.ToString());
            EditorGUILayout.LabelField(
                "centerline / frames / boxes",
                stats.CenterlinePointCount + " / " + stats.FrameCount + " / " + stats.BoxCount);
            EditorGUILayout.LabelField(
                "trainPose / cars",
                (stats.HasTrainPose ? "present" : "absent") + " / " + stats.TrainPoseCarCount);
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

            DrawViewerPrefabStatus();

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

            DrawGeneratedSelectionActions();
        }

        private void DrawViewerPrefabStatus()
        {
            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                FindViewerTransformVisualizerForDisplay();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Prefab Status", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(transformVisualizer == null))
            {
                EditorGUILayout.LabelField(
                    "Body Prefab",
                    FormatAssignedStatus(transformVisualizer != null && transformVisualizer.HasTrainBodyPrefab));
                EditorGUILayout.LabelField(
                    "Banking Profile Prefab",
                    FormatAssignedStatus(
                        transformVisualizer != null && transformVisualizer.HasBankingProfileBodyPrefab));
                EditorGUILayout.LabelField(
                    "Bogie Prefab",
                    FormatAssignedStatus(transformVisualizer != null && transformVisualizer.HasBogiePrefab));
                EditorGUILayout.LabelField(
                    "Wheel Prefab",
                    FormatAssignedStatus(transformVisualizer != null && transformVisualizer.HasWheelPrefab));
            }

            if (transformVisualizer == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a viewer to inspect DebugViewportSnapshotV1 prefab slots.",
                    MessageType.Info);
            }
        }

        private void DrawGeneratedSelectionActions()
        {
            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                FindViewerTransformVisualizerForDisplay();
            Transform generatedRoot = transformVisualizer != null
                ? transformVisualizer.FindGeneratedHierarchy()
                : null;
            Transform bodyInstances = transformVisualizer != null
                ? transformVisualizer.FindTrainBodyInstances()
                : null;
            Transform bankingProfileInstances = transformVisualizer != null
                ? transformVisualizer.FindBankingProfileBodyInstances()
                : null;

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(generatedRoot == null))
            {
                if (GUILayout.Button("Select Generated Hierarchy", GUILayout.Height(28f)))
                {
                    SelectGeneratedHierarchy();
                }
            }

            using (new EditorGUI.DisabledScope(bodyInstances == null || bodyInstances.childCount == 0))
            {
                if (GUILayout.Button("Select Body Instances", GUILayout.Height(28f)))
                {
                    SelectBodyInstances();
                }
            }

            using (new EditorGUI.DisabledScope(
                bankingProfileInstances == null || bankingProfileInstances.childCount == 0))
            {
                if (GUILayout.Button("Select Banking Profile Instances", GUILayout.Height(28f)))
                {
                    SelectBankingProfileInstances();
                }
            }

            EditorGUILayout.EndHorizontal();
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

        private void InitializeArtifactSourceFolder()
        {
            if (!string.IsNullOrWhiteSpace(_artifactSourceFolder))
            {
                return;
            }

            string savedSourceFolder = EditorPrefs.GetString(ArtifactSourceEditorPrefsKey, string.Empty);
            _artifactSourceFolder = string.IsNullOrWhiteSpace(savedSourceFolder)
                ? GeneratedArtifactDefaultSourcePath
                : savedSourceFolder;
        }

        private void BrowseArtifactSourceFolder()
        {
            string startFolder;
            try
            {
                startFolder = ResolveFolderInput(_artifactSourceFolder);
            }
            catch (Exception)
            {
                startFolder = GetProjectRootPath();
            }

            if (!Directory.Exists(startFolder))
            {
                startFolder = GetProjectRootPath();
            }

            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Generated DebugViewport Artifacts",
                startFolder,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                return;
            }

            _artifactSourceFolder = selectedFolder;
            EditorPrefs.SetString(ArtifactSourceEditorPrefsKey, _artifactSourceFolder);
        }

        private void ImportGeneratedArtifacts()
        {
            try
            {
                string sourcePath = ResolveFolderInput(_artifactSourceFolder);
                if (!Directory.Exists(sourcePath))
                {
                    throw new DirectoryNotFoundException(
                        "Generated artifact source folder was not found at '" + sourcePath +
                        "'. Run .\\tools\\demo-technical-preview-0.1.cmd first, or choose a folder containing generated JSON, SVG, and HTML artifacts.");
                }

                string targetPath = AssetPathToFullPath(DebugDataFolderAssetPath);
                if (AreSameDirectory(sourcePath, targetPath))
                {
                    throw new InvalidOperationException(
                        "Source folder and target folder are both '" + targetPath +
                        "'. Choose the backend artifact folder outside Assets/DebugData.");
                }

                Directory.CreateDirectory(targetPath);

                int cleanedFileCount = _cleanBeforeImport
                    ? CleanDebugDataArtifacts(targetPath)
                    : 0;

                ArtifactImportResult result = CopyGeneratedArtifacts(sourcePath, targetPath, cleanedFileCount);
                AssetDatabase.Refresh();
                RefreshSnapshotList();
                RefreshSelectedSnapshotReference();

                if (_snapshotJson != null)
                {
                    LoadSelectedSnapshot();
                }

                string copiedSummary =
                    "JSON copied: " + result.JsonCopied +
                    ", SVG copied: " + result.SvgCopied +
                    ", HTML copied: " + result.HtmlCopied +
                    ", cleaned: " + result.CleanedFileCount + ".";

                if (result.TotalCopied == 0)
                {
                    _lastArtifactImportText =
                        "No matching *.json, *.svg, or *.html files were found in '" + sourcePath + "'. " +
                        copiedSummary;
                    _lastArtifactImportType = MessageType.Warning;
                    _statusText = _lastArtifactImportText;
                    _statusType = MessageType.Warning;
                    return;
                }

                _lastArtifactImportText =
                    "Imported generated artifacts into " + DebugDataFolderAssetPath + ". " + copiedSummary;
                _lastArtifactImportType = MessageType.Info;
                _statusText = _lastArtifactImportText;
                _statusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                _lastArtifactImportText = "Generated artifact import failed: " + ex.Message;
                _lastArtifactImportType = MessageType.Warning;
                _statusText = _lastArtifactImportText;
                _statusType = MessageType.Warning;
            }
        }

        private void RefreshSnapshotList()
        {
            _snapshotListItems.Clear();
            _hasDebugDataArtifacts = HasGeneratedDebugDataArtifacts();

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

                    SnapshotInspectionResult inspection = InspectSnapshot(asset);
                    _snapshotListItems.Add(new SnapshotListItem(
                        asset,
                        path,
                        knownSample.Label,
                        true,
                        inspection.IsValid,
                        inspection.Error,
                        knownSample.Group,
                        inspection.Stats));
                    includedPaths.Add(path);
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

                SnapshotInspectionResult inspection = InspectSnapshot(asset);
                if (!inspection.IsValid)
                {
                    if (IsDebugDataAssetPath(path))
                    {
                        _snapshotListItems.Add(new SnapshotListItem(
                            asset,
                            path,
                            string.Empty,
                            false,
                            false,
                            inspection.Error,
                            SnapshotSourceGroup.InvalidDebugDataJson,
                            SnapshotStats.Empty));
                    }

                    continue;
                }

                _snapshotListItems.Add(new SnapshotListItem(
                    asset,
                    path,
                    string.Empty,
                    false,
                    true,
                    string.Empty,
                    ResolveValidSnapshotGroup(path, inspection.Stats),
                    inspection.Stats));
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

        private void SelectGeneratedHierarchy()
        {
            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                ResolveTransformVisualizerForAction();
            if (transformVisualizer == null)
            {
                return;
            }

            Transform generatedRoot = transformVisualizer.FindGeneratedHierarchy();
            if (generatedRoot == null)
            {
                _statusText = "No GeneratedSnapshot hierarchy was found. Rebuild generated boxes first.";
                _statusType = MessageType.Warning;
                return;
            }

            Selection.activeGameObject = generatedRoot.gameObject;
            EditorGUIUtility.PingObject(generatedRoot.gameObject);
            _statusText = "Selected " + generatedRoot.name + ".";
            _statusType = MessageType.Info;
        }

        private void SelectBodyInstances()
        {
            SelectGeneratedRoleInstances(
                DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
                "body");
        }

        private void SelectBankingProfileInstances()
        {
            SelectGeneratedRoleInstances(
                DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole,
                "banking profile");
        }

        private void SelectGeneratedRoleInstances(string role, string label)
        {
            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                ResolveTransformVisualizerForAction();
            if (transformVisualizer == null)
            {
                return;
            }

            Transform group = transformVisualizer.FindGeneratedRoleGroup(role);
            if (group == null || group.childCount == 0)
            {
                _statusText = "No generated " + label + " instances were found. Rebuild generated boxes first.";
                _statusType = MessageType.Warning;
                return;
            }

            var selectedObjects = new UnityEngine.Object[group.childCount];
            for (int i = 0; i < group.childCount; i++)
            {
                selectedObjects[i] = group.GetChild(i).gameObject;
            }

            Selection.objects = selectedObjects;
            Selection.activeGameObject = group.GetChild(0).gameObject;
            EditorGUIUtility.PingObject(group.GetChild(0).gameObject);
            _statusText = "Selected " + group.childCount + " generated " + label + " instance(s).";
            _statusType = MessageType.Info;
        }

        private DebugViewportSnapshotV1TransformVisualizer ResolveTransformVisualizerForAction()
        {
            GameObject viewer = ResolveViewerGameObject(createIfMissing: false);
            if (viewer == null)
            {
                _statusText = "No Quantum Snapshot Viewer is assigned or found.";
                _statusType = MessageType.Warning;
                return null;
            }

            _viewerGameObject = viewer;
            DebugViewportSnapshotV1TransformVisualizer transformVisualizer =
                viewer.GetComponent<DebugViewportSnapshotV1TransformVisualizer>();
            if (transformVisualizer == null)
            {
                _statusText = viewer.name + " does not have a DebugViewportSnapshotV1TransformVisualizer.";
                _statusType = MessageType.Warning;
                return null;
            }

            return transformVisualizer;
        }

        private DebugViewportSnapshotV1TransformVisualizer FindViewerTransformVisualizerForDisplay()
        {
            GameObject viewer = GetAssignedViewer();
            if (viewer == null)
            {
                viewer = FindViewerOnSelection();
            }

            if (viewer == null)
            {
                viewer = FindViewerInOpenScenes();
            }

            return viewer != null
                ? viewer.GetComponent<DebugViewportSnapshotV1TransformVisualizer>()
                : null;
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

        private static SnapshotInspectionResult InspectSnapshot(TextAsset asset)
        {
            if (DebugViewportSnapshotV1JsonLoader.TryLoad(
                asset,
                out DebugViewportSnapshotV1Dto snapshot,
                out string error))
            {
                return new SnapshotInspectionResult(true, string.Empty, SnapshotStats.From(snapshot));
            }

            return new SnapshotInspectionResult(false, error, SnapshotStats.Empty);
        }

        private int CountSnapshotListItems(SnapshotSourceGroup group)
        {
            int count = 0;
            for (int i = 0; i < _snapshotListItems.Count; i++)
            {
                if (_snapshotListItems[i].Group == group)
                {
                    count++;
                }
            }

            return count;
        }

        private static SnapshotSourceGroup ResolveValidSnapshotGroup(string assetPath, SnapshotStats stats)
        {
            return IsCsvFixtureSnapshot(assetPath, stats)
                ? SnapshotSourceGroup.CsvFixtures
                : SnapshotSourceGroup.OtherValidSnapshots;
        }

        private static bool IsCsvFixtureSnapshot(string assetPath, SnapshotStats stats)
        {
            if (!string.IsNullOrWhiteSpace(stats.SourceFixtureName) &&
                stats.SourceFixtureName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string fileName = Path.GetFileName(assetPath);
            return fileName.StartsWith("Milestone7.synthetic.", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".snapshot.json", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetGroupLabel(SnapshotSourceGroup group)
        {
            switch (group)
            {
                case SnapshotSourceGroup.BuiltIn:
                    return "Built-in";
                case SnapshotSourceGroup.BankingProfile:
                    return "BankingProfile";
                case SnapshotSourceGroup.CsvFixtures:
                    return "CSV fixtures";
                case SnapshotSourceGroup.OtherValidSnapshots:
                    return "Other valid snapshots";
                case SnapshotSourceGroup.InvalidDebugDataJson:
                    return "Invalid/unknown DebugData JSON";
                default:
                    return "Snapshots";
            }
        }

        private static ArtifactImportResult CopyGeneratedArtifacts(
            string sourcePath,
            string targetPath,
            int cleanedFileCount)
        {
            return new ArtifactImportResult(
                CopyGeneratedArtifacts(sourcePath, targetPath, "*.json"),
                CopyGeneratedArtifacts(sourcePath, targetPath, "*.svg"),
                CopyGeneratedArtifacts(sourcePath, targetPath, "*.html"),
                cleanedFileCount);
        }

        private static int CopyGeneratedArtifacts(string sourcePath, string targetPath, string searchPattern)
        {
            string[] files = Directory.GetFiles(sourcePath, searchPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int count = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string targetFilePath = Path.Combine(targetPath, Path.GetFileName(files[i]));
                File.Copy(files[i], targetFilePath, true);
                count++;
            }

            return count;
        }

        private static int CleanDebugDataArtifacts(string targetPath)
        {
            int count = 0;
            count += DeleteFiles(targetPath, "*.json");
            count += DeleteFiles(targetPath, "*.svg");
            count += DeleteFiles(targetPath, "*.html");
            return count;
        }

        private static int DeleteFiles(string folderPath, string searchPattern)
        {
            if (!Directory.Exists(folderPath))
            {
                return 0;
            }

            string[] files = Directory.GetFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly);
            int count = 0;
            for (int i = 0; i < files.Length; i++)
            {
                File.Delete(files[i]);
                count++;
            }

            return count;
        }

        private static bool HasGeneratedDebugDataArtifacts()
        {
            string debugDataPath = AssetPathToFullPath(DebugDataFolderAssetPath);
            if (!Directory.Exists(debugDataPath))
            {
                return false;
            }

            return Directory.GetFiles(debugDataPath, "*.json", SearchOption.TopDirectoryOnly).Length > 0 ||
                Directory.GetFiles(debugDataPath, "*.svg", SearchOption.TopDirectoryOnly).Length > 0 ||
                Directory.GetFiles(debugDataPath, "*.html", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static string ResolveFolderInput(string folderInput)
        {
            if (string.IsNullOrWhiteSpace(folderInput))
            {
                throw new InvalidOperationException("Source folder is empty.");
            }

            if (Path.IsPathRooted(folderInput))
            {
                return Path.GetFullPath(folderInput);
            }

            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), folderInput));
        }

        private static bool AreSameDirectory(string firstPath, string secondPath)
        {
            string firstFullPath = NormalizeDirectoryPath(firstPath);
            string secondFullPath = NormalizeDirectoryPath(secondPath);
            return string.Equals(firstFullPath, secondFullPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            string normalizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), normalizedAssetPath));
        }

        private static string GetProjectRootPath()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            return projectRoot != null ? projectRoot.FullName : Application.dataPath;
        }

        private static string DisplayText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
        }

        private static string FormatAssignedStatus(bool assigned)
        {
            return assigned ? "Assigned" : "Missing";
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

        private enum SnapshotSourceGroup
        {
            BuiltIn,
            BankingProfile,
            CsvFixtures,
            OtherValidSnapshots,
            InvalidDebugDataJson
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
            public KnownSnapshotSample(string label, string fileName, SnapshotSourceGroup group)
            {
                Label = label;
                FileName = fileName;
                Group = group;
            }

            public string Label { get; }

            public string FileName { get; }

            public SnapshotSourceGroup Group { get; }
        }

        private readonly struct SnapshotListItem
        {
            public SnapshotListItem(
                TextAsset asset,
                string assetPath,
                string knownLabel,
                bool isKnown,
                bool isValid,
                string error,
                SnapshotSourceGroup group,
                SnapshotStats stats)
            {
                Asset = asset;
                AssetPath = assetPath;
                KnownLabel = knownLabel;
                IsKnown = isKnown;
                IsValid = isValid;
                Error = error;
                Group = group;
                Stats = stats;
            }

            public TextAsset Asset { get; }

            public string AssetPath { get; }

            public string KnownLabel { get; }

            public bool IsKnown { get; }

            public bool IsValid { get; }

            public string Error { get; }

            public SnapshotSourceGroup Group { get; }

            public SnapshotStats Stats { get; }
        }

        private readonly struct SnapshotInspectionResult
        {
            public SnapshotInspectionResult(bool isValid, string error, SnapshotStats stats)
            {
                IsValid = isValid;
                Error = error;
                Stats = stats;
            }

            public bool IsValid { get; }

            public string Error { get; }

            public SnapshotStats Stats { get; }
        }

        private readonly struct ArtifactImportResult
        {
            public ArtifactImportResult(
                int jsonCopied,
                int svgCopied,
                int htmlCopied,
                int cleanedFileCount)
            {
                JsonCopied = jsonCopied;
                SvgCopied = svgCopied;
                HtmlCopied = htmlCopied;
                CleanedFileCount = cleanedFileCount;
            }

            public int JsonCopied { get; }

            public int SvgCopied { get; }

            public int HtmlCopied { get; }

            public int CleanedFileCount { get; }

            public int TotalCopied
            {
                get { return JsonCopied + SvgCopied + HtmlCopied; }
            }
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

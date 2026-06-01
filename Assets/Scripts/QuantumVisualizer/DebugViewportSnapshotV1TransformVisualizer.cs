using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumVisualizer
{
    /// <summary>
    /// Generates a deterministic GameObject hierarchy from DebugViewportSnapshotV1 boxes.
    /// Centerlines, frame axes, and debug lines intentionally remain in DebugViewportSnapshotV1GizmoVisualizer.
    /// </summary>
    [ExecuteAlways]
    public sealed class DebugViewportSnapshotV1TransformVisualizer : MonoBehaviour
    {
        private const string GeneratedRootName = "GeneratedSnapshot";
        private const string FallbackCubeName = "FallbackCube";
        private const string PrefabInstanceName = "Prefab";
        private const float MinimumDimension = 0.01f;

        private static readonly string[] RoleGroupNames =
        {
            DebugViewportSnapshotV1Vocabulary.TrainBodyRole,
            DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole,
            DebugViewportSnapshotV1Vocabulary.TrainBogieRole,
            DebugViewportSnapshotV1Vocabulary.TrainWheelRole,
            "unknown"
        };

        [Header("Input")]
        [SerializeField] private TextAsset snapshotJson;
        [SerializeField] private bool rebuildAutomatically = true;
        [SerializeField] private bool logParseErrors = true;
        [SerializeField] private bool logGeneratedSummary = true;

        [Header("Optional Prefab Slots")]
        [SerializeField, Tooltip("Optional visual prefab for train.body boxes. The generated box wrapper remains driven by the snapshot frame and size; this prefab is instantiated below it at local identity.")]
        private GameObject trainBodyPrefab;
        [SerializeField, Tooltip("Optional visual prefab for train.body.banking-profile boxes. The generated box wrapper remains driven by the snapshot frame and size; this prefab is instantiated below it at local identity.")]
        private GameObject bankingProfileBodyPrefab;
        [SerializeField, Tooltip("Optional visual prefab for train.bogie boxes. The generated box wrapper remains driven by the snapshot frame and size; this prefab is instantiated below it at local identity.")]
        private GameObject bogiePrefab;
        [SerializeField, Tooltip("Optional visual prefab for train.wheel boxes. The generated box wrapper remains driven by the snapshot frame and size; this prefab is instantiated below it at local identity.")]
        private GameObject wheelPrefab;
        [SerializeField, Tooltip("Optional visual prefab for unknown-role boxes. The generated box wrapper remains driven by the snapshot frame and size; this prefab is instantiated below it at local identity.")]
        private GameObject unknownPrefab;

        [Header("Fallback Colors")]
        [SerializeField] private Color trainBodyColor = new Color(0.25f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color bankingProfileBodyColor = new Color(0.95f, 0.45f, 1f, 1f);
        [SerializeField] private Color bogieColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color wheelColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color unknownColor = Color.white;

        [NonSerialized] private readonly List<Material> _generatedMaterials = new List<Material>();
        private bool _rebuildQueued = true;
        private string _builtJsonText;
        private string _lastError;

        private void OnEnable()
        {
            QueueRebuild();
        }

        private void OnValidate()
        {
            QueueRebuild();
        }

        private void Update()
        {
            if (!rebuildAutomatically)
            {
                return;
            }

            if (_rebuildQueued || HasJsonChanged())
            {
                Rebuild();
            }
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            _rebuildQueued = false;
            _builtJsonText = snapshotJson != null ? snapshotJson.text : null;

            Clear();

            if (!DebugViewportSnapshotV1JsonLoader.TryLoad(
                snapshotJson,
                out DebugViewportSnapshotV1Dto snapshot,
                out string error))
            {
                LogLoadError(error);
                return;
            }

            _lastError = null;

            GameObject generatedRoot = CreateEmpty(GeneratedRootName, transform);
            Transform generatedRootTransform = generatedRoot.transform;

            Transform[] groupTransforms = CreateRoleGroups(generatedRootTransform);
            GeneratedSnapshotSummary summary = BuildGeneratedSnapshot(groupTransforms, snapshot);
            ReportGeneratedSummary(summary);
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            DestroyGeneratedMaterials();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (string.Equals(child.name, GeneratedRootName, StringComparison.Ordinal))
                {
                    DestroyGeneratedObject(child.gameObject);
                }
            }
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
        }

        private bool HasJsonChanged()
        {
            string currentText = snapshotJson != null ? snapshotJson.text : null;
            return !string.Equals(currentText, _builtJsonText, StringComparison.Ordinal);
        }

        private static Transform[] CreateRoleGroups(Transform generatedRoot)
        {
            var transforms = new Transform[RoleGroupNames.Length];

            for (int i = 0; i < RoleGroupNames.Length; i++)
            {
                transforms[i] = CreateEmpty(RoleGroupNames[i], generatedRoot).transform;
            }

            return transforms;
        }

        private GeneratedSnapshotSummary BuildGeneratedSnapshot(
            Transform[] groupTransforms,
            DebugViewportSnapshotV1Dto snapshot)
        {
            var summary = new GeneratedSnapshotSummary();

            if (snapshot == null || snapshot.boxes == null)
            {
                return summary;
            }

            Material[] fallbackMaterials =
            {
                CreateMaterial("Quantum Snapshot train.body", trainBodyColor),
                CreateMaterial("Quantum Snapshot train.body.banking-profile", bankingProfileBodyColor),
                CreateMaterial("Quantum Snapshot train.bogie", bogieColor),
                CreateMaterial("Quantum Snapshot train.wheel", wheelColor),
                CreateMaterial("Quantum Snapshot unknown", unknownColor)
            };

            int[] roleLocalIndices = new int[RoleGroupNames.Length];

            for (int i = 0; i < snapshot.boxes.Length; i++)
            {
                DebugViewportBoxV1Dto box = snapshot.boxes[i];
                if (!TryResolveBoxPose(box, out Vector3 position, out Quaternion rotation, out Vector3 size))
                {
                    summary.Skipped++;
                    continue;
                }

                int groupIndex = ResolveRoleGroupIndex(box.role);
                int localIndex = roleLocalIndices[groupIndex];
                roleLocalIndices[groupIndex] = localIndex + 1;

                GameObject boxWrapper = CreateEmpty(
                    BuildBoxName(localIndex, box.label),
                    groupTransforms[groupIndex]);

                boxWrapper.transform.localPosition = position;
                boxWrapper.transform.localRotation = rotation;
                boxWrapper.transform.localScale = size;

                GameObject prefab = GetPrefab(groupIndex);
                if (prefab == null)
                {
                    CreateFallbackCube(boxWrapper.transform, fallbackMaterials[groupIndex]);
                }
                else
                {
                    InstantiatePrefabVisual(prefab, boxWrapper.transform);
                    summary.PrefabInstances++;
                }

                summary.Total++;
                summary.RoleCounts[groupIndex]++;
            }

            return summary;
        }

        private static bool TryResolveBoxPose(
            DebugViewportBoxV1Dto box,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 size)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            size = Vector3.one;

            if (box == null || box.frame == null || box.frame.position == null || box.size == null)
            {
                return false;
            }

            position = ToVector3(box.frame.position);
            if (!IsFinite(position))
            {
                return false;
            }

            if (!TryCreateRotation(box.frame, out rotation))
            {
                rotation = Quaternion.identity;
            }

            size = new Vector3(
                PositiveOrDefault(box.size.length, MinimumDimension),
                PositiveOrDefault(box.size.height, MinimumDimension),
                PositiveOrDefault(box.size.width, MinimumDimension));

            return IsFinite(size);
        }

        private GameObject GetPrefab(int groupIndex)
        {
            if (groupIndex == 0)
            {
                return trainBodyPrefab;
            }

            if (groupIndex == 1)
            {
                return bankingProfileBodyPrefab;
            }

            if (groupIndex == 2)
            {
                return bogiePrefab;
            }

            if (groupIndex == 3)
            {
                return wheelPrefab;
            }

            return unknownPrefab;
        }

        private static int ResolveRoleGroupIndex(string role)
        {
            for (int i = 0; i < RoleGroupNames.Length - 1; i++)
            {
                if (string.Equals(role, RoleGroupNames[i], StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return RoleGroupNames.Length - 1;
        }

        private static string BuildBoxName(int localIndex, string label)
        {
            string name = "Box_" + FormatIndex(localIndex);
            string suffix = SanitizeName(label);

            if (!string.IsNullOrEmpty(suffix))
            {
                name += "_" + suffix;
            }

            return name;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static GameObject CreateEmpty(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;
            return gameObject;
        }

        private GameObject CreateFallbackCube(Transform parent, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = FallbackCubeName;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one;

            Collider primitiveCollider = gameObject.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                DestroyGeneratedObject(primitiveCollider);
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return gameObject;
        }

        private static GameObject InstantiatePrefabVisual(GameObject prefab, Transform parent)
        {
            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = PrefabInstanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            _generatedMaterials.Add(material);
            return material;
        }

        private void DestroyGeneratedMaterials()
        {
            for (int i = _generatedMaterials.Count - 1; i >= 0; i--)
            {
                Material material = _generatedMaterials[i];
                if (material != null)
                {
                    DestroyGeneratedObject(material);
                }
            }

            _generatedMaterials.Clear();
        }

        private void ReportGeneratedSummary(GeneratedSnapshotSummary summary)
        {
            if (!logGeneratedSummary)
            {
                return;
            }

            string text = summary.ToLogText();
            Debug.Log("DebugViewportSnapshotV1TransformVisualizer: " + text, this);
        }

        private void LogLoadError(string error)
        {
            if (!logParseErrors || string.Equals(_lastError, error, StringComparison.Ordinal))
            {
                return;
            }

            Debug.LogWarning("DebugViewportSnapshotV1TransformVisualizer: " + error, this);
            _lastError = error;
        }

        private static bool TryCreateRotation(DebugViewportFrameV1Dto frame, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (frame == null)
            {
                return false;
            }

            Vector3 tangent = ToVector3(frame.tangent);
            Vector3 normal = ToVector3(frame.normal);
            Vector3 binormal = ToVector3(frame.binormal);

            if (!IsUsableDirection(normal))
            {
                return false;
            }

            normal.Normalize();

            if (!IsUsableDirection(binormal))
            {
                if (!IsUsableDirection(tangent))
                {
                    return false;
                }

                tangent.Normalize();
                binormal = Vector3.Cross(tangent, normal);
            }

            if (!IsUsableDirection(binormal))
            {
                return false;
            }

            binormal.Normalize();

            if (Mathf.Abs(Vector3.Dot(normal, binormal)) > 0.999f)
            {
                if (!IsUsableDirection(tangent))
                {
                    return false;
                }

                tangent.Normalize();
                binormal = Vector3.Cross(tangent, normal);
                if (!IsUsableDirection(binormal))
                {
                    return false;
                }

                binormal.Normalize();
            }

            rotation = Quaternion.LookRotation(binormal, normal);
            return true;
        }

        private static Vector3 ToVector3(DebugViewportVector3V1Dto source)
        {
            if (source == null)
            {
                return Vector3.zero;
            }

            return new Vector3(source.x, source.y, source.z);
        }

        private static bool IsUsableDirection(Vector3 vector)
        {
            return IsFinite(vector) && vector.sqrMagnitude > 0.000001f;
        }

        private static bool IsFinite(Vector3 vector)
        {
            return IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float PositiveOrDefault(float value, float fallback)
        {
            if (!IsFinite(value) || value < MinimumDimension)
            {
                return Mathf.Max(MinimumDimension, fallback);
            }

            return value;
        }

        private static string FormatIndex(int index)
        {
            return index.ToString("000");
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private sealed class GeneratedSnapshotSummary
        {
            public readonly int[] RoleCounts = new int[RoleGroupNames.Length];
            public int Total;
            public int PrefabInstances;
            public int Skipped;

            public string ToLogText()
            {
                return "generated boxes=" + Total +
                    " (train.body=" + RoleCounts[0] +
                    ", train.body.banking-profile=" + RoleCounts[1] +
                    ", train.bogie=" + RoleCounts[2] +
                    ", train.wheel=" + RoleCounts[3] +
                    ", unknown=" + RoleCounts[4] +
                    "), prefabInstances=" + PrefabInstances +
                    ", skipped=" + Skipped + ".";
            }
        }
    }
}

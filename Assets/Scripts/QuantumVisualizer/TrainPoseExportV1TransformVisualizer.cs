using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuantumVisualizer
{
    /// <summary>
    /// Visual-only placeholder hierarchy for TrainPoseExportV1 JSON snapshots.
    /// </summary>
    [ExecuteAlways]
    public sealed class TrainPoseExportV1TransformVisualizer : MonoBehaviour
    {
        private const string GeneratedRootName = "GeneratedPose";
        private const float MinimumDimension = 0.01f;

        [Header("Input")]
        [SerializeField] private TextAsset poseJson;
        [SerializeField] private bool rebuildAutomatically = true;
        [SerializeField] private bool logParseErrors = true;

        [Header("Generated Placeholders")]
        [SerializeField] private bool createBodyPlaceholders = true;
        [SerializeField] private bool createBogiePlaceholders = true;
        [SerializeField] private bool createWheelPlaceholders = true;
        [SerializeField] private bool createArticulationMarkers = true;

        [Header("Sizing")]
        [SerializeField, Min(0.01f)] private float bodyScaleMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float fallbackBodyLength = 4f;
        [SerializeField, Min(0.01f)] private float fallbackBodyWidth = 1.6f;
        [SerializeField, Min(0.01f)] private float fallbackBodyHeight = 1.4f;
        [SerializeField, Min(0.01f)] private float fallbackBogieLength = 0.9f;
        [SerializeField, Min(0.01f)] private float fallbackBogieWidth = 1.1f;
        [SerializeField, Min(0.01f)] private float bogieHeight = 0.18f;
        [SerializeField, Min(0.01f)] private float fallbackWheelRadius = 0.25f;
        [SerializeField, Min(0.01f)] private float fallbackWheelWidth = 0.15f;
        [SerializeField, Min(0.01f)] private float articulationMarkerRadius = 0.12f;

        [Header("Colors")]
        [SerializeField] private Color bodyColor = new Color(0.25f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color bogieColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color wheelColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color articulationColor = new Color(1f, 0.25f, 0.35f, 1f);

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
                RebuildGeneratedPose();
            }
        }

        [ContextMenu("Rebuild Generated Pose")]
        public void RebuildGeneratedPose()
        {
            _rebuildQueued = false;
            _builtJsonText = poseJson != null ? poseJson.text : null;

            ClearGeneratedPose();

            if (!TrainPoseJsonLoader.TryLoad(poseJson, out TrainPoseExportV1Dto pose, out string error))
            {
                LogLoadError(error);
                return;
            }

            _lastError = null;

            GameObject generatedRoot = CreateEmpty(GeneratedRootName, transform);
            generatedRoot.transform.localPosition = Vector3.zero;
            generatedRoot.transform.localRotation = Quaternion.identity;
            generatedRoot.transform.localScale = Vector3.one;

            BuildGeneratedPose(generatedRoot.transform, pose);
        }

        [ContextMenu("Clear Generated Pose")]
        public void ClearGeneratedPose()
        {
            DestroyGeneratedMaterials();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (string.Equals(child.name, GeneratedRootName, StringComparison.Ordinal))
                {
                    DestroyObject(child.gameObject);
                }
            }
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
        }

        private bool HasJsonChanged()
        {
            string currentText = poseJson != null ? poseJson.text : null;
            return !string.Equals(currentText, _builtJsonText, StringComparison.Ordinal);
        }

        private void BuildGeneratedPose(Transform generatedRoot, TrainPoseExportV1Dto pose)
        {
            if (pose == null || pose.cars == null)
            {
                return;
            }

            Material bodyMaterial = CreateMaterial("Quantum Body Placeholder", bodyColor);
            Material bogieMaterial = CreateMaterial("Quantum Bogie Placeholder", bogieColor);
            Material wheelMaterial = CreateMaterial("Quantum Wheel Placeholder", wheelColor);
            Material articulationMaterial = CreateMaterial("Quantum Articulation Placeholder", articulationColor);

            TrainCarGeometryV1Dto geometry = pose.definition != null ? pose.definition.carGeometry : null;
            TrainWheelLayoutV1Dto wheelLayout = pose.definition != null ? pose.definition.wheelLayout : null;

            for (int i = 0; i < pose.cars.Length; i++)
            {
                ArticulatedTrainCarWithWheelsV1Dto car = pose.cars[i];
                if (car == null)
                {
                    continue;
                }

                int carIndex = ResolveCarIndex(car, i);
                GameObject carRoot = CreateEmpty("Car_" + FormatIndex(carIndex), generatedRoot);

                BuildBody(carRoot.transform, car.body, geometry, bodyMaterial, articulationMaterial);

                GameObject bogiesRoot = CreateEmpty("Bogies", carRoot.transform);
                BuildBogie(bogiesRoot.transform, "Front", car.frontBogie, geometry, wheelLayout, bogieMaterial, wheelMaterial);
                BuildBogie(bogiesRoot.transform, "Rear", car.rearBogie, geometry, wheelLayout, bogieMaterial, wheelMaterial);
            }
        }

        private void BuildBody(
            Transform carRoot,
            ArticulatedTrainCarV1Dto body,
            TrainCarGeometryV1Dto geometry,
            Material bodyMaterial,
            Material articulationMaterial)
        {
            if (body == null)
            {
                return;
            }

            if (createBodyPlaceholders)
            {
                GameObject bodyObject = CreatePrimitive(PrimitiveType.Cube, "Body_ArticulatedMatrix", carRoot, bodyMaterial);
                ApplyLocalPose(bodyObject.transform, body.articulatedMatrix, body.articulatedFrame);
                bodyObject.transform.localScale = GetBodyScale(geometry);
            }

            if (createArticulationMarkers)
            {
                GameObject markersRoot = CreateEmpty("ArticulationMarkers", carRoot);
                GameObject marker = CreatePrimitive(PrimitiveType.Sphere, "Center_ArticulatedMatrix", markersRoot.transform, articulationMaterial);
                ApplyLocalPose(marker.transform, body.articulatedMatrix, body.articulatedFrame);
                float diameter = PositiveOrDefault(articulationMarkerRadius, 0.12f) * 2f;
                marker.transform.localScale = new Vector3(diameter, diameter, diameter);
            }
        }

        private void BuildBogie(
            Transform bogiesRoot,
            string role,
            TrainBogieWithWheelsV1Dto bogieWithWheels,
            TrainCarGeometryV1Dto geometry,
            TrainWheelLayoutV1Dto wheelLayout,
            Material bogieMaterial,
            Material wheelMaterial)
        {
            if (bogieWithWheels == null || bogieWithWheels.bogie == null)
            {
                return;
            }

            BogieTransformV1Dto bogie = bogieWithWheels.bogie;
            GameObject bogieRoot = CreateEmpty(role + "Bogie_B" + FormatIndex(bogie.bogieIndex), bogiesRoot);
            ApplyLocalPose(bogieRoot.transform, bogie.matrix, bogie.frame);

            if (createBogiePlaceholders)
            {
                GameObject bogieBox = CreatePrimitive(PrimitiveType.Cube, "BogieBox", bogieRoot.transform, bogieMaterial);
                bogieBox.transform.localPosition = Vector3.zero;
                bogieBox.transform.localRotation = Quaternion.identity;
                bogieBox.transform.localScale = GetBogieScale(geometry, wheelLayout);
            }

            if (!createWheelPlaceholders || bogieWithWheels.wheels == null)
            {
                return;
            }

            GameObject wheelsRoot = CreateEmpty("Wheels", bogieRoot.transform);
            for (int i = 0; i < bogieWithWheels.wheels.Length; i++)
            {
                WheelTransformV1Dto wheel = bogieWithWheels.wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                BuildWheel(wheelsRoot.transform, wheel, wheelLayout, wheelMaterial);
            }
        }

        private void BuildWheel(
            Transform wheelsRoot,
            WheelTransformV1Dto wheel,
            TrainWheelLayoutV1Dto wheelLayout,
            Material wheelMaterial)
        {
            GameObject wheelObject = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Wheel_" + FormatIndex(wheel.wheelIndex),
                wheelsRoot,
                wheelMaterial);

            wheelObject.transform.localPosition = new Vector3(
                wheel.localOffsetX,
                wheel.localOffsetY,
                wheel.localOffsetZ);
            wheelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wheelObject.transform.localScale = GetWheelScale(wheelLayout);
        }

        private static int ResolveCarIndex(ArticulatedTrainCarWithWheelsV1Dto car, int fallbackIndex)
        {
            if (car.body != null && car.body.originalBody != null)
            {
                return car.body.originalBody.carIndex;
            }

            if (car.frontBogie != null && car.frontBogie.bogie != null)
            {
                return car.frontBogie.bogie.carIndex;
            }

            if (car.rearBogie != null && car.rearBogie.bogie != null)
            {
                return car.rearBogie.bogie.carIndex;
            }

            return fallbackIndex;
        }

        private Vector3 GetBodyScale(TrainCarGeometryV1Dto geometry)
        {
            float length = PositiveOrDefault(geometry != null ? geometry.length : 0f, fallbackBodyLength);
            float width = PositiveOrDefault(geometry != null ? geometry.width : 0f, fallbackBodyWidth);
            float height = PositiveOrDefault(geometry != null ? geometry.height : 0f, fallbackBodyHeight);
            float multiplier = PositiveOrDefault(bodyScaleMultiplier, 1f);

            return new Vector3(length * multiplier, height * multiplier, width * multiplier);
        }

        private Vector3 GetBogieScale(TrainCarGeometryV1Dto geometry, TrainWheelLayoutV1Dto wheelLayout)
        {
            float bodyWidth = PositiveOrDefault(geometry != null ? geometry.width : 0f, fallbackBogieWidth);
            float radius = PositiveOrDefault(wheelLayout != null ? wheelLayout.wheelRadius : 0f, fallbackWheelRadius);
            float axleSpacing = PositiveOrDefault(wheelLayout != null ? wheelLayout.axleSpacing : 0f, fallbackBogieLength);

            float length = Mathf.Max(fallbackBogieLength, axleSpacing + (radius * 2f));
            float width = Mathf.Max(fallbackBogieWidth, bodyWidth * 0.75f);
            float height = Mathf.Max(PositiveOrDefault(bogieHeight, 0.18f), radius * 0.35f);

            return new Vector3(length, height, width);
        }

        private Vector3 GetWheelScale(TrainWheelLayoutV1Dto wheelLayout)
        {
            float radius = PositiveOrDefault(wheelLayout != null ? wheelLayout.wheelRadius : 0f, fallbackWheelRadius);
            float width = PositiveOrDefault(wheelLayout != null ? wheelLayout.wheelWidth : 0f, fallbackWheelWidth);
            float diameter = radius * 2f;

            // Unity cylinders are 2 units tall on local Y before rotation.
            return new Vector3(diameter, width * 0.5f, diameter);
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

        private GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);

            Collider primitiveCollider = gameObject.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                DestroyObject(primitiveCollider);
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return gameObject;
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
                    DestroyObject(material);
                }
            }

            _generatedMaterials.Clear();
        }

        private static void ApplyLocalPose(Transform target, Matrix4x4V1Dto matrix, TrackFrameV1Dto fallbackFrame)
        {
            if (target == null)
            {
                return;
            }

            Vector3 position;
            Quaternion rotation;

            if (matrix != null)
            {
                position = ExtractPosition(matrix);

                if (TryExtractRotation(matrix, out rotation) ||
                    TryExtractRotation(fallbackFrame, out rotation))
                {
                    target.localPosition = position;
                    target.localRotation = rotation;
                    return;
                }

                target.localPosition = position;
                target.localRotation = Quaternion.identity;
                return;
            }

            if (TryExtractPose(fallbackFrame, out position, out rotation))
            {
                target.localPosition = position;
                target.localRotation = rotation;
            }
        }

        private static Vector3 ExtractPosition(Matrix4x4V1Dto matrix)
        {
            return new Vector3(matrix.m14, matrix.m24, matrix.m34);
        }

        private static bool TryExtractRotation(Matrix4x4V1Dto matrix, out Quaternion rotation)
        {
            Vector3 tangent = new Vector3(matrix.m11, matrix.m21, matrix.m31);
            Vector3 normal = new Vector3(matrix.m12, matrix.m22, matrix.m32);
            Vector3 binormal = new Vector3(matrix.m13, matrix.m23, matrix.m33);

            return TryCreateRotation(tangent, normal, binormal, out rotation);
        }

        private static bool TryExtractPose(TrackFrameV1Dto frame, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            if (frame == null || frame.position == null)
            {
                return false;
            }

            position = ToVector3(frame.position);
            Vector3 tangent = ToVector3(frame.tangent);
            Vector3 normal = ToVector3(frame.normal);
            Vector3 binormal = ToVector3(frame.binormal);

            return TryCreateRotation(tangent, normal, binormal, out rotation);
        }

        private static bool TryExtractRotation(TrackFrameV1Dto frame, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (frame == null)
            {
                return false;
            }

            Vector3 tangent = ToVector3(frame.tangent);
            Vector3 normal = ToVector3(frame.normal);
            Vector3 binormal = ToVector3(frame.binormal);

            return TryCreateRotation(tangent, normal, binormal, out rotation);
        }

        private static bool TryCreateRotation(
            Vector3 tangent,
            Vector3 normal,
            Vector3 binormal,
            out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            if (!IsUsableDirection(normal) || !IsUsableDirection(binormal))
            {
                return false;
            }

            normal.Normalize();
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

        private static Vector3 ToVector3(Vector3V1Dto source)
        {
            if (source == null)
            {
                return Vector3.zero;
            }

            return new Vector3(source.x, source.y, source.z);
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
            return index.ToString("00");
        }

        private void LogLoadError(string error)
        {
            if (!logParseErrors || string.Equals(_lastError, error, StringComparison.Ordinal))
            {
                return;
            }

            Debug.LogWarning("TrainPoseExportV1TransformVisualizer: " + error, this);
            _lastError = error;
        }

        private static void DestroyObject(UnityEngine.Object target)
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
    }
}

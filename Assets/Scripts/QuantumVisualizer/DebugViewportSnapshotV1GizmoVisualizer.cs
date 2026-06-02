using System;
using UnityEngine;

namespace QuantumVisualizer
{
    [ExecuteAlways]
    public sealed class DebugViewportSnapshotV1GizmoVisualizer : MonoBehaviour
    {
        private const float MinimumDimension = 0.01f;

        [Header("Input")]
        [SerializeField] private TextAsset snapshotJson;
        [SerializeField] private bool drawOnlyWhenSelected;
        [SerializeField] private bool logParseErrors = true;
        [SerializeField] private bool logSnapshotSummary = true;

        [Header("Layers")]
        [SerializeField] private bool drawCenterline = true;
        [SerializeField] private bool drawFrames = true;
        [SerializeField] private bool drawSnapshotLines = true;
        [SerializeField] private bool drawBoxes = true;

        [Header("Line Kinds")]
        [SerializeField] private bool drawTangentLines = true;
        [SerializeField] private bool drawNormalLines = true;
        [SerializeField] private bool drawBinormalLines = true;
        [SerializeField] private bool drawDiagnosticLines = true;
        [SerializeField] private bool drawUnknownLineKinds = true;

        [Header("Box Roles")]
        [SerializeField] private bool drawTrainBodyBoxes = true;
        [SerializeField] private bool drawBankingProfileBodyBoxes = true;
        [SerializeField] private bool drawBogieBoxes = true;
        [SerializeField] private bool drawWheelBoxes = true;
        [SerializeField] private bool drawUnknownBoxRoles = true;

        [Header("Style")]
        [SerializeField, Min(0.01f)] private float frameAxisLength = 0.75f;
        [SerializeField, Min(0.0f)] private float centerlinePointRadius = 0.0f;
        [SerializeField, Min(0.0f)] private float frameOriginRadius = 0.03f;
        [SerializeField, Min(0.0f)] private float boxCenterRadius = 0.0f;
        [SerializeField] private Color centerlineColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private Color tangentColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color normalColor = new Color(0.35f, 1f, 0.35f, 1f);
        [SerializeField] private Color binormalColor = new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color diagnosticLineColor = new Color(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private Color unknownLineColor = Color.white;
        [SerializeField] private Color trainBodyColor = new Color(0.25f, 0.55f, 0.95f, 1f);
        [SerializeField] private Color bankingProfileBodyColor = new Color(0.95f, 0.45f, 1f, 1f);
        [SerializeField] private Color bogieColor = new Color(0.95f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color wheelColor = new Color(0.08f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color unknownBoxColor = Color.white;

        private bool _hasCachedParse;
        private bool _cachedLoadSucceeded;
        private string _cachedJsonText;
        private DebugViewportSnapshotV1Dto _cachedSnapshot;
        private string _lastError;
        private string _lastSummary;

        public TextAsset SnapshotJson
        {
            get { return snapshotJson; }
        }

        public void ApplySnapshot(TextAsset jsonAsset)
        {
            snapshotJson = jsonAsset;
            ClearCachedParse();
        }

        private void OnValidate()
        {
            frameAxisLength = Mathf.Max(0.01f, frameAxisLength);
            centerlinePointRadius = Mathf.Max(0.0f, centerlinePointRadius);
            frameOriginRadius = Mathf.Max(0.0f, frameOriginRadius);
            boxCenterRadius = Mathf.Max(0.0f, boxCenterRadius);
            ClearCachedParse();
        }

        private void OnDrawGizmos()
        {
            if (!drawOnlyWhenSelected)
            {
                DrawSnapshotGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnlyWhenSelected)
            {
                DrawSnapshotGizmos();
            }
        }

        private void DrawSnapshotGizmos()
        {
            if (!TryGetSnapshot(out DebugViewportSnapshotV1Dto snapshot))
            {
                return;
            }

            if (drawCenterline)
            {
                DrawCenterlinePolyline(snapshot.centerlinePoints);
            }

            if (drawFrames)
            {
                DrawFrameAxes(snapshot.frames);
            }

            if (drawSnapshotLines)
            {
                DrawSnapshotLines(snapshot.lines);
            }

            if (drawBoxes)
            {
                DrawSnapshotBoxes(snapshot.boxes);
            }
        }

        private void DrawCenterlinePolyline(DebugViewportCenterlinePointV1Dto[] points)
        {
            if (points == null || points.Length == 0)
            {
                return;
            }

            Gizmos.color = centerlineColor;

            bool hasPrevious = false;
            Vector3 previous = Vector3.zero;

            for (int i = 0; i < points.Length; i++)
            {
                DebugViewportCenterlinePointV1Dto point = points[i];
                if (point == null || point.position == null)
                {
                    hasPrevious = false;
                    continue;
                }

                Vector3 current = ToVector3(point.position);
                if (!IsFinite(current))
                {
                    hasPrevious = false;
                    continue;
                }

                if (hasPrevious)
                {
                    Gizmos.DrawLine(previous, current);
                }

                if (centerlinePointRadius > 0f)
                {
                    Gizmos.DrawSphere(current, centerlinePointRadius);
                }

                previous = current;
                hasPrevious = true;
            }
        }

        private void DrawFrameAxes(DebugViewportFrameV1Dto[] frames)
        {
            if (frames == null)
            {
                return;
            }

            for (int i = 0; i < frames.Length; i++)
            {
                DrawFrameAxes(frames[i], frameAxisLength);
            }
        }

        private void DrawFrameAxes(DebugViewportFrameV1Dto frame, float axisLength)
        {
            if (frame == null || frame.position == null)
            {
                return;
            }

            Vector3 p = ToVector3(frame.position);
            if (!IsFinite(p))
            {
                return;
            }

            Vector3 t = NormalizeOrFallback(frame.tangent, Vector3.right);
            Vector3 n = NormalizeOrFallback(frame.normal, Vector3.up);
            Vector3 b = NormalizeOrFallback(frame.binormal, Vector3.forward);

            Gizmos.color = tangentColor;
            Gizmos.DrawLine(p, p + t * axisLength);
            Gizmos.color = normalColor;
            Gizmos.DrawLine(p, p + n * axisLength);
            Gizmos.color = binormalColor;
            Gizmos.DrawLine(p, p + b * axisLength);

            if (frameOriginRadius > 0f)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(p, frameOriginRadius);
            }
        }

        private void DrawSnapshotLines(DebugViewportLineSegmentV1Dto[] lines)
        {
            if (lines == null)
            {
                return;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                DebugViewportLineSegmentV1Dto line = lines[i];
                if (line == null || line.start == null || line.end == null || !ShouldDrawLineKind(line.kind))
                {
                    continue;
                }

                Vector3 start = ToVector3(line.start);
                Vector3 end = ToVector3(line.end);
                if (!IsFinite(start) || !IsFinite(end))
                {
                    continue;
                }

                Gizmos.color = GetLineColor(line.kind);
                Gizmos.DrawLine(start, end);
            }
        }

        private void DrawSnapshotBoxes(DebugViewportBoxV1Dto[] boxes)
        {
            if (boxes == null)
            {
                return;
            }

            for (int i = 0; i < boxes.Length; i++)
            {
                DrawSnapshotBox(boxes[i]);
            }
        }

        private void DrawSnapshotBox(DebugViewportBoxV1Dto box)
        {
            if (box == null ||
                box.frame == null ||
                box.frame.position == null ||
                box.size == null ||
                !ShouldDrawBoxRole(box.role))
            {
                return;
            }

            Vector3 center = ToVector3(box.frame.position);
            if (!IsFinite(center))
            {
                return;
            }

            Vector3 size = new Vector3(
                PositiveOrDefault(box.size.length, MinimumDimension),
                PositiveOrDefault(box.size.height, MinimumDimension),
                PositiveOrDefault(box.size.width, MinimumDimension));

            Quaternion rotation;
            if (!TryCreateRotation(box.frame, out rotation))
            {
                rotation = Quaternion.identity;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.color = GetBoxColor(box.role);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previousMatrix;

            if (boxCenterRadius > 0f)
            {
                Gizmos.color = GetBoxColor(box.role);
                Gizmos.DrawSphere(center, boxCenterRadius);
            }
        }

        private bool TryGetSnapshot(out DebugViewportSnapshotV1Dto snapshot)
        {
            snapshot = null;

            if (snapshotJson == null)
            {
                return false;
            }

            string text = snapshotJson.text;
            if (_hasCachedParse && string.Equals(text, _cachedJsonText, StringComparison.Ordinal))
            {
                snapshot = _cachedLoadSucceeded ? _cachedSnapshot : null;
                return _cachedLoadSucceeded;
            }

            _hasCachedParse = true;
            _cachedJsonText = text;

            if (!DebugViewportSnapshotV1JsonLoader.TryLoad(snapshotJson, out DebugViewportSnapshotV1Dto loaded, out string error))
            {
                if (logParseErrors && !string.Equals(_lastError, error, StringComparison.Ordinal))
                {
                    Debug.LogWarning("DebugViewportSnapshotV1GizmoVisualizer: " + error, this);
                    _lastError = error;
                }

                _cachedLoadSucceeded = false;
                _cachedSnapshot = null;
                return false;
            }

            _lastError = null;
            _cachedLoadSucceeded = true;
            _cachedSnapshot = loaded;
            snapshot = loaded;

            ReportSnapshotSummary(loaded);
            return true;
        }

        private void ReportSnapshotSummary(DebugViewportSnapshotV1Dto snapshot)
        {
            if (!logSnapshotSummary)
            {
                return;
            }

            string summary = BuildSnapshotSummary(snapshot);
            if (string.Equals(summary, _lastSummary, StringComparison.Ordinal))
            {
                return;
            }

            _lastSummary = summary;
            Debug.Log("DebugViewportSnapshotV1GizmoVisualizer: " + summary, this);
        }

        private static string BuildSnapshotSummary(DebugViewportSnapshotV1Dto snapshot)
        {
            int centerlinePointCount = snapshot != null && snapshot.centerlinePoints != null ? snapshot.centerlinePoints.Length : 0;
            int frameCount = snapshot != null && snapshot.frames != null ? snapshot.frames.Length : 0;
            int lineCount = snapshot != null && snapshot.lines != null ? snapshot.lines.Length : 0;
            int boxCount = snapshot != null && snapshot.boxes != null ? snapshot.boxes.Length : 0;
            bool hasTrainPose = snapshot != null && snapshot.trainPose != null;
            int trainPoseCarCount = hasTrainPose ? ResolveTrainPoseCarCount(snapshot.trainPose) : 0;

            return "loaded centerlinePoints=" + centerlinePointCount +
                ", frames=" + frameCount +
                ", lines=" + lineCount +
                ", boxes=" + boxCount +
                ", trainPose=" + (hasTrainPose ? "present" : "absent") +
                ", trainPoseCars=" + trainPoseCarCount + ".";
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

        private bool ShouldDrawLineKind(string kind)
        {
            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisTangentKind, StringComparison.Ordinal))
            {
                return drawTangentLines;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisNormalKind, StringComparison.Ordinal))
            {
                return drawNormalLines;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisBinormalKind, StringComparison.Ordinal))
            {
                return drawBinormalLines;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.DiagnosticLineKind, StringComparison.Ordinal))
            {
                return drawDiagnosticLines;
            }

            return drawUnknownLineKinds;
        }

        private Color GetLineColor(string kind)
        {
            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisTangentKind, StringComparison.Ordinal))
            {
                return tangentColor;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisNormalKind, StringComparison.Ordinal))
            {
                return normalColor;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.FrameAxisBinormalKind, StringComparison.Ordinal))
            {
                return binormalColor;
            }

            if (string.Equals(kind, DebugViewportSnapshotV1Vocabulary.DiagnosticLineKind, StringComparison.Ordinal))
            {
                return diagnosticLineColor;
            }

            return unknownLineColor;
        }

        private bool ShouldDrawBoxRole(string role)
        {
            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyRole, StringComparison.Ordinal))
            {
                return drawTrainBodyBoxes;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole, StringComparison.Ordinal))
            {
                return drawBankingProfileBodyBoxes;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBogieRole, StringComparison.Ordinal))
            {
                return drawBogieBoxes;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainWheelRole, StringComparison.Ordinal))
            {
                return drawWheelBoxes;
            }

            return drawUnknownBoxRoles;
        }

        private Color GetBoxColor(string role)
        {
            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyRole, StringComparison.Ordinal))
            {
                return trainBodyColor;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBodyBankingProfileRole, StringComparison.Ordinal))
            {
                return bankingProfileBodyColor;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainBogieRole, StringComparison.Ordinal))
            {
                return bogieColor;
            }

            if (string.Equals(role, DebugViewportSnapshotV1Vocabulary.TrainWheelRole, StringComparison.Ordinal))
            {
                return wheelColor;
            }

            return unknownBoxColor;
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

        private static Vector3 NormalizeOrFallback(DebugViewportVector3V1Dto source, Vector3 fallback)
        {
            Vector3 vector = ToVector3(source);
            if (!IsUsableDirection(vector))
            {
                return fallback;
            }

            return vector.normalized;
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

        private void ClearCachedParse()
        {
            _hasCachedParse = false;
            _cachedLoadSucceeded = false;
            _cachedJsonText = null;
            _cachedSnapshot = null;
            _lastError = null;
            _lastSummary = null;
        }
    }
}

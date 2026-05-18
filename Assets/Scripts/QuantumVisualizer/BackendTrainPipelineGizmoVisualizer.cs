using System;
using System.Collections.Generic;
using System.Text;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using UnityEngine;
using UnityEngine.Serialization;
using TrackFrame = Quantum.Track.TrackFrame;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace QuantumVisualizer
{
    [ExecuteAlways]
    public sealed class BackendTrainPipelineGizmoVisualizer : MonoBehaviour
    {
        private enum DebugTrackMode
        {
            SegmentedCurrent = 0,
            SmoothContinuousSpline = 1
        }

        [Header("Input")]
        [SerializeField] private bool drawOnlyWhenSelected;

        [Header("Sampling")]
        [SerializeField, Min(2)] private int centerlineSampleCount = 128;
        [SerializeField, Min(1)] private int gizmoSubSamplesPerSegment = 1;
        [SerializeField] private DebugTrackMode debugTrackMode = DebugTrackMode.SegmentedCurrent;
        [SerializeField, Min(4)] private int smoothSplineControlPointSampleCount = 32;
        [SerializeField, Min(8)] private int smoothSplineArcLengthSampleCount = 384;
        [SerializeField, Min(0.0f)] private float smoothRollBlendDistance = 8.0f;
        [SerializeField, Min(1)] private int carCount = 8;
        [SerializeField, Min(0.01f)] private float carSpacing = 6f;

        [Header("Train Box")]
        [SerializeField, Min(0.01f)] private float carLength = 8f;
        [SerializeField, Min(0.01f)] private float carWidth = 1.8f;
        [SerializeField, Min(0.01f)] private float carHeight = 2.2f;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] private float playhead01 = 0.75f;
        [SerializeField] private bool autoPlay;
        [SerializeField, Min(0.0f)] private float playbackSpeed = 0.08f;
        [SerializeField] private bool loopPlayback = true;
        [SerializeField] private float leadDistanceOffset;
        [SerializeField] private bool clampLeadDistance = true;

        [Header("Feature Toggles")]
        [SerializeField] private bool drawTrackCenterline = true;
        [SerializeField] private bool drawHeartline = true;
        [SerializeField] private bool drawBankingRibbon = true;
        [FormerlySerializedAs("drawRailDebugLines")]
        [SerializeField] private bool drawRails;
        [SerializeField] private bool generateDebugMeshPreview;
        [FormerlySerializedAs("drawRailCrossTies")]
        [SerializeField] private bool drawCrossTies = true;
        [SerializeField] private bool drawCarWireBoxes = true;
        [SerializeField] private bool drawCarAxes = true;
        [SerializeField] private bool drawCarSpacing = true;
        [SerializeField] private bool useSmoothDebugFramesForTrainPlacement;
        [SerializeField] private bool drawBogieMarkers = true;
        [SerializeField] private bool drawWheelMarkers = true;
        [SerializeField] private bool drawArticulationCenterPoints = true;
        [SerializeField] private bool drawCouplerConnections = true;
        [SerializeField] private bool drawDebugHud;
        [SerializeField] private bool drawSmoothnessDiagnosticsHud;
        [SerializeField] private bool logSmoothnessDiagnostics;
        [SerializeField, Min(2)] private int smoothnessDiagnosticsSampleCount = 256;
        [SerializeField] private bool logTrainSamplingDiagnostics;
        [SerializeField, Min(0.0f)] private float trainSamplingDiagnosticsIntervalSeconds = 0.0f;

        [Header("Track")]
        [SerializeField, Min(0.01f)] private float trackGauge = 1.435f;
        [SerializeField, Min(0.01f)] private float railCrossTieSpacing = 2.0f;

        [Header("Banking Ribbon")]
        [SerializeField, Min(0.01f)] private float bankingRibbonHalfWidth = 1.0f;
        [SerializeField] private float bankingRibbonNormalOffset = 0.6f;

        [Header("Style")]
        [SerializeField, Min(0.01f)] private float axisLength = 1.0f;
        [SerializeField] private float heartlineHeight = 1.0f;
        [SerializeField, Min(0.0f)] private float spacingTolerance = 0.001f;
        [SerializeField, Min(0.0f)] private float spacingDotRadius = 0.05f;
        [SerializeField, Min(0.0f)] private float detailMarkerRadius = 0.06f;
        [SerializeField, Min(0.0f)] private float debugHudVerticalOffset = 1.5f;

        [SerializeField] private Color tangentColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color normalColor = new Color(0.35f, 1f, 0.35f, 1f);
        [SerializeField] private Color binormalColor = new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color trackColor = new Color(0.2f, 0.95f, 1f, 1f);
        [SerializeField] private Color heartlineColor = new Color(1f, 0.25f, 0.7f, 1f);
        [SerializeField] private Color bankingRibbonColor = new Color(0.2f, 0.85f, 0.95f, 1f);
        [FormerlySerializedAs("railCrossTieColor")]
        [SerializeField] private Color crossTieColor = new Color(0.9f, 0.8f, 0.55f, 1f);
        [SerializeField] private Color spacingColorGood = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color spacingColorWarning = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color bogieMarkerColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color wheelMarkerColor = new Color(1f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color articulationMarkerColor = new Color(0.95f, 0.35f, 1f, 1f);
        [SerializeField] private Color couplerColor = new Color(1f, 0.95f, 0.45f, 1f);

        private TrackDocument _document;
        private TrackEvaluator _evaluator;
        private TrainCarTransformProvider _provider;

        private int _cachedDistanceSampleCount;
        private double _cachedDistanceTotalLength = double.NaN;
        private double[] _cachedCenterlineDistances;
        private int _cachedGizmoDistanceSampleCount;
        private int _cachedGizmoSubSamplesPerSegment;
        private double _cachedGizmoDistanceTotalLength = double.NaN;
        private double[] _cachedGizmoDistances;
        private int _cachedSmoothControlPointSampleCount;
        private int _cachedSmoothArcLengthSampleCount;
        private double _cachedSmoothRollBlendDistance = double.NaN;
        private double[] _cachedSmoothFrameDistances;
        private TrackFrame[] _cachedSmoothFrames;

        private string _lastError;
        private TrackFrameSmoothnessReport _lastSmoothnessReport;
        private TrackFrameSmoothnessReport _lastSegmentedSmoothnessReport;
        private TrackFrameSmoothnessReport _lastSmoothContinuousSmoothnessReport;
        private float _lastRuntimeUpdateTime = float.NaN;
        private double _lastLeadDistanceForSpeed = double.NaN;
        private double _lastLeadDistanceSampleTime = double.NaN;
        private double _lastSampledLeadSpeed;
        private bool _hasSampledLeadSpeed;
        private bool _hasLoggedSmoothnessDiagnostics;
        private double _lastTrainSamplingDiagnosticsLogTime = double.NaN;
        private TrainSamplingDiagnosticsState _segmentedTrainSamplingDiagnosticsState;
        private TrainSamplingDiagnosticsState _smoothTrainSamplingDiagnosticsState;
        private Mesh _debugTrackPreviewMesh;
#if UNITY_EDITOR
        private double _lastEditorUpdateTime = double.NaN;
#endif

        private readonly struct TrainFrameSnapshot
        {
            public TrainFrameSnapshot(int carIndex, double distance, TrackFrame frame)
            {
                CarIndex = carIndex;
                Distance = distance;
                Frame = frame;
            }

            public int CarIndex { get; }

            public double Distance { get; }

            public TrackFrame Frame { get; }
        }

        private struct TrainSamplingDiagnosticsState
        {
            public double LastSampleTime;

            public double LastLeadDistance;

            public TrainFrameSnapshot[] LastCarSnapshots;
        }

        private readonly struct TrainMotionDeltaStats
        {
            public TrainMotionDeltaStats(
                bool hasPreviousSample,
                double deltaTimeSeconds,
                double leadDistanceDelta,
                int comparedCars,
                int previousCarCount,
                double maxCarPositionDelta,
                double averageCarPositionDelta,
                double maxCarFrameDeltaDegrees,
                double averageCarFrameDeltaDegrees)
            {
                HasPreviousSample = hasPreviousSample;
                DeltaTimeSeconds = deltaTimeSeconds;
                LeadDistanceDelta = leadDistanceDelta;
                ComparedCars = comparedCars;
                PreviousCarCount = previousCarCount;
                MaxCarPositionDelta = maxCarPositionDelta;
                AverageCarPositionDelta = averageCarPositionDelta;
                MaxCarFrameDeltaDegrees = maxCarFrameDeltaDegrees;
                AverageCarFrameDeltaDegrees = averageCarFrameDeltaDegrees;
            }

            public bool HasPreviousSample { get; }

            public double DeltaTimeSeconds { get; }

            public double LeadDistanceDelta { get; }

            public int ComparedCars { get; }

            public int PreviousCarCount { get; }

            public double MaxCarPositionDelta { get; }

            public double AverageCarPositionDelta { get; }

            public double MaxCarFrameDeltaDegrees { get; }

            public double AverageCarFrameDeltaDegrees { get; }
        }

        private readonly struct FrameDeltaMetrics
        {
            public FrameDeltaMetrics(
                double positionDelta,
                double tangentDeltaDegrees,
                double normalDeltaDegrees,
                double binormalDeltaDegrees)
            {
                PositionDelta = positionDelta;
                TangentDeltaDegrees = tangentDeltaDegrees;
                NormalDeltaDegrees = normalDeltaDegrees;
                BinormalDeltaDegrees = binormalDeltaDegrees;
            }

            public double PositionDelta { get; }

            public double TangentDeltaDegrees { get; }

            public double NormalDeltaDegrees { get; }

            public double BinormalDeltaDegrees { get; }

            public double MaxAxisDeltaDegrees => System.Math.Max(
                TangentDeltaDegrees,
                System.Math.Max(NormalDeltaDegrees, BinormalDeltaDegrees));
        }

        private void OnValidate()
        {
            centerlineSampleCount = Mathf.Max(2, centerlineSampleCount);
            gizmoSubSamplesPerSegment = Mathf.Max(1, gizmoSubSamplesPerSegment);
            smoothSplineControlPointSampleCount = Mathf.Max(4, smoothSplineControlPointSampleCount);
            smoothSplineArcLengthSampleCount = Mathf.Max(8, smoothSplineArcLengthSampleCount);
            smoothRollBlendDistance = Mathf.Max(0.0f, smoothRollBlendDistance);
            smoothnessDiagnosticsSampleCount = Mathf.Max(2, smoothnessDiagnosticsSampleCount);
            trainSamplingDiagnosticsIntervalSeconds = Mathf.Max(0.0f, trainSamplingDiagnosticsIntervalSeconds);
            carCount = Mathf.Max(1, carCount);
            carSpacing = Mathf.Max(0.01f, carSpacing);
            carLength = Mathf.Max(0.01f, carLength);
            carWidth = Mathf.Max(0.01f, carWidth);
            carHeight = Mathf.Max(0.01f, carHeight);
            trackGauge = Mathf.Max(0.01f, trackGauge);
            railCrossTieSpacing = Mathf.Max(0.01f, railCrossTieSpacing);
            bankingRibbonHalfWidth = Mathf.Max(0.01f, bankingRibbonHalfWidth);
            if (float.IsNaN(bankingRibbonNormalOffset) || float.IsInfinity(bankingRibbonNormalOffset))
            {
                bankingRibbonNormalOffset = 0.0f;
            }
            if (float.IsNaN(heartlineHeight) || float.IsInfinity(heartlineHeight))
            {
                heartlineHeight = 0.0f;
            }
            playbackSpeed = Mathf.Max(0.0f, playbackSpeed);
            axisLength = Mathf.Max(0.01f, axisLength);
            spacingTolerance = Mathf.Max(0.0f, spacingTolerance);
            spacingDotRadius = Mathf.Max(0.0f, spacingDotRadius);
            detailMarkerRadius = Mathf.Max(0.0f, detailMarkerRadius);
            debugHudVerticalOffset = Mathf.Max(0.0f, debugHudVerticalOffset);
            playhead01 = Mathf.Clamp01(playhead01);

            _cachedDistanceSampleCount = 0;
            _cachedDistanceTotalLength = double.NaN;
            _cachedCenterlineDistances = null;
            _cachedGizmoDistanceSampleCount = 0;
            _cachedGizmoSubSamplesPerSegment = 0;
            _cachedGizmoDistanceTotalLength = double.NaN;
            _cachedGizmoDistances = null;
            ResetSmoothFrameSamplingCache();
            _lastError = null;
            _lastSmoothnessReport = null;
            _lastSegmentedSmoothnessReport = null;
            _lastSmoothContinuousSmoothnessReport = null;
            _hasLoggedSmoothnessDiagnostics = false;
            ResetPlaybackSamplingState();
            ResetTrainSamplingDiagnosticsState();

            if (!generateDebugMeshPreview)
            {
                DestroyDebugTrackPreviewMesh();
            }
            else
            {
                ClearDebugTrackPreviewMesh();
            }
        }

        private void OnEnable()
        {
            ResetPlaybackSamplingState();
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
#endif
        }

        private void OnDisable()
        {
            DestroyDebugTrackPreviewMesh();
#if UNITY_EDITOR
            EditorApplication.update -= OnEditorUpdate;
#endif
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                _lastRuntimeUpdateTime = float.NaN;
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (float.IsNaN(_lastRuntimeUpdateTime))
            {
                _lastRuntimeUpdateTime = now;
                return;
            }

            float deltaTime = Mathf.Max(0.0f, now - _lastRuntimeUpdateTime);
            _lastRuntimeUpdateTime = now;
            AdvancePlayhead(deltaTime);
        }

        private void OnDrawGizmos()
        {
            if (!drawOnlyWhenSelected)
            {
                DrawPipelineGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnlyWhenSelected)
            {
                DrawPipelineGizmos();
            }
        }

#if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            if (Application.isPlaying || !isActiveAndEnabled)
            {
                _lastEditorUpdateTime = double.NaN;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (double.IsNaN(_lastEditorUpdateTime))
            {
                _lastEditorUpdateTime = now;
                return;
            }

            float deltaTime = (float)System.Math.Max(0.0, now - _lastEditorUpdateTime);
            _lastEditorUpdateTime = now;

            if (AdvancePlayhead(deltaTime))
            {
                SceneView.RepaintAll();
            }
        }
#endif

        private void DrawPipelineGizmos()
        {
            if (!TryEnsurePipeline())
            {
                DestroyDebugTrackPreviewMesh();
                return;
            }

            try
            {
                double totalLength = _document.TotalLength;
                bool requiresDetailPose = drawBogieMarkers || drawWheelMarkers || drawArticulationCenterPoints || drawCouplerConnections;
                TrainConsistDefinition definition = null;
                double minLeadDistance = (carCount - 1) * carSpacing;
                double maxLeadDistance = totalLength;

                if (requiresDetailPose)
                {
                    definition = BuildDerivedConsistDefinition();
                    double halfBogieSpacing = definition.BogieSpacing * 0.5;
                    minLeadDistance += halfBogieSpacing;
                    maxLeadDistance -= halfBogieSpacing;
                }

                if (minLeadDistance > maxLeadDistance)
                {
                    ReportWarningOnce("Train configuration requires more lead distance than the deterministic sample track length allows for the enabled visualization toggles.");
                    return;
                }

                double requestedLeadDistance = (playhead01 * totalLength) + leadDistanceOffset;
                double leadDistance = clampLeadDistance
                    ? Clamp(requestedLeadDistance, minLeadDistance, maxLeadDistance)
                    : requestedLeadDistance;
                IReadOnlyList<TrainCarTransform> segmentedCars;
                IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> articulatedCars = null;

                if (requiresDetailPose)
                {
                    articulatedCars = _provider.EvaluateArticulatedTrainWithWheels(leadDistance, definition);
                    segmentedCars = ExtractBodyTransforms(articulatedCars);
                }
                else
                {
                    segmentedCars = _provider.GetCarTransforms(
                        leadDistance,
                        carSpacing,
                        carCount);
                }

                IReadOnlyList<TrainCarTransform> smoothDebugFrameCars = null;
                bool smoothDebugFrameCarsAvailable = false;
                if (useSmoothDebugFramesForTrainPlacement || logTrainSamplingDiagnostics)
                {
                    smoothDebugFrameCarsAvailable = TryBuildSmoothDebugFrameTrainPlacement(
                        segmentedCars,
                        out smoothDebugFrameCars);
                }

                IReadOnlyList<TrainCarTransform> cars = segmentedCars;
                bool usingSmoothTrainPlacement = false;
                string trainBodyFramePathLabel = "TrackEvaluator.EvaluateFramesAtDistances(segmented)";
                if (useSmoothDebugFramesForTrainPlacement)
                {
                    if (smoothDebugFrameCarsAvailable)
                    {
                        cars = smoothDebugFrameCars;
                        usingSmoothTrainPlacement = true;
                        trainBodyFramePathLabel = "SmoothContinuousSpline(debug)";
                    }
                    else
                    {
                        trainBodyFramePathLabel = "SmoothContinuousSpline(debug fallback->segmented)";
                    }
                }

                _lastError = null;
                UpdateSampledLeadSpeed(leadDistance);
                CaptureTrainSamplingDiagnostics(
                    leadDistance,
                    segmentedCars,
                    smoothDebugFrameCarsAvailable ? smoothDebugFrameCars : null,
                    cars,
                    usingSmoothTrainPlacement,
                    trainBodyFramePathLabel,
                    articulatedCars);

                if (drawTrackCenterline || drawHeartline || drawBankingRibbon || drawRails || drawCrossTies || generateDebugMeshPreview)
                {
                    DrawTrackSamples(totalLength);
                }
                else
                {
                    ClearDebugTrackPreviewMesh();
                }

                if (drawCarWireBoxes)
                {
                    DrawCarWireBoxes(cars);
                }

                if (drawCarAxes)
                {
                    DrawCarAxes(cars);
                }

                if (drawCarSpacing)
                {
                    DrawSpacingLinks(cars);
                }

                if (articulatedCars != null)
                {
                    if (drawBogieMarkers)
                    {
                        DrawBogieMarkers(articulatedCars);
                    }

                    if (drawWheelMarkers)
                    {
                        DrawWheelMarkers(articulatedCars);
                    }

                    if (drawArticulationCenterPoints)
                    {
                        DrawArticulationCenters(articulatedCars);
                    }

                    if (drawCouplerConnections)
                    {
                        DrawCouplerConnections(articulatedCars);
                    }
                }

                DrawDebugHudLabel(cars, totalLength, requestedLeadDistance, leadDistance);
            }
            catch (Exception ex)
            {
                DestroyDebugTrackPreviewMesh();
                ReportWarningOnce(ex.Message);
            }
        }

        private bool TryEnsurePipeline()
        {
            if (_provider != null)
            {
                return true;
            }

            try
            {
                _document = BuildDeterministicDocument();
                _evaluator = new TrackEvaluator(_document);
                _provider = new TrainCarTransformProvider(_evaluator);
                _lastError = null;
                _hasLoggedSmoothnessDiagnostics = false;
                ResetSmoothFrameSamplingCache();
                return true;
            }
            catch (Exception ex)
            {
                ReportWarningOnce("Failed to build deterministic backend pipeline: " + ex.Message);
                return false;
            }
        }

        private static TrackDocument BuildDeterministicDocument()
        {
            TrackSegment[] segments =
            {
                new StraightSegment(
                    length: 52.0,
                    id: "s0",
                    spline: new LineCurve(
                        new Vector3d(0.0, 0.0, 0.0),
                        new Vector3d(52.0, 6.0, 0.0)),
                    rollRadians: 0.0),
                new CurvedSegment(
                    length: 92.0,
                    id: "c1",
                    spline: new CubicBezierCurve(
                        new Vector3d(52.0, 6.0, 0.0),
                        new Vector3d(80.0, 10.0, 3.0),
                        new Vector3d(105.0, 31.0, 45.0),
                        new Vector3d(122.0, 34.0, 66.0)),
                    rollRadians: 0.18),
                new CurvedSegment(
                    length: 94.0,
                    id: "c2",
                    spline: new CubicBezierCurve(
                        new Vector3d(122.0, 34.0, 66.0),
                        new Vector3d(139.0, 37.0, 87.0),
                        new Vector3d(157.0, 28.0, 36.0),
                        new Vector3d(176.0, 24.0, 22.0)),
                    rollRadians: 0.34),
                new CurvedSegment(
                    length: 76.0,
                    id: "c3",
                    spline: new CubicBezierCurve(
                        new Vector3d(176.0, 24.0, 22.0),
                        new Vector3d(195.0, 20.0, 8.0),
                        new Vector3d(220.0, 12.0, -8.0),
                        new Vector3d(244.0, 10.0, -6.0)),
                    rollRadians: 0.16),
                new StraightSegment(
                    length: 54.0,
                    id: "s4",
                    spline: new LineCurve(
                        new Vector3d(244.0, 10.0, -6.0),
                        new Vector3d(298.0, 8.0, -6.0)),
                    rollRadians: 0.06)
            };

            return new TrackDocument(segments);
        }

        private void DrawTrackSamples(double totalLength)
        {
            double[] centerlineDistances = GetCenterlineDistances(totalLength);
            TrackFrame[] segmentedCenterlineFrames = _evaluator.EvaluateFramesAtDistances(centerlineDistances);
            TrackFrame[] segmentedRenderFrames = segmentedCenterlineFrames;
            IReadOnlyList<double> renderDistances = centerlineDistances;

            if (gizmoSubSamplesPerSegment > 1 &&
                (drawHeartline || drawBankingRibbon || drawRails || generateDebugMeshPreview))
            {
                double[] gizmoDistances = GetGizmoSubsampleDistances(totalLength);
                segmentedRenderFrames = _evaluator.EvaluateFramesAtDistances(gizmoDistances);
                renderDistances = gizmoDistances;
            }

            TrackFrame[] centerlineFramesForDraw = segmentedCenterlineFrames;
            TrackFrame[] renderFramesForDraw = segmentedRenderFrames;

            if (debugTrackMode == DebugTrackMode.SmoothContinuousSpline)
            {
                bool centerlineSmoothAvailable = TryEvaluateSmoothFramesAtDistances(
                    centerlineDistances,
                    out TrackFrame[] smoothCenterlineFrames);
                TrackFrame[] smoothRenderFrames = smoothCenterlineFrames;
                bool renderSmoothAvailable = true;
                if (renderDistances != centerlineDistances)
                {
                    renderSmoothAvailable = TryEvaluateSmoothFramesAtDistances(
                        renderDistances,
                        out smoothRenderFrames);
                }

                if (centerlineSmoothAvailable && renderSmoothAvailable)
                {
                    centerlineFramesForDraw = smoothCenterlineFrames;
                    renderFramesForDraw = renderDistances == centerlineDistances
                        ? smoothCenterlineFrames
                        : smoothRenderFrames;
                }
            }

            CaptureSmoothnessDiagnostics(totalLength, renderFramesForDraw, renderDistances);

            if (generateDebugMeshPreview)
            {
                DrawDebugTrackMeshPreview(renderFramesForDraw);
            }
            else
            {
                ClearDebugTrackPreviewMesh();
            }

            if (drawTrackCenterline)
            {
                Gizmos.color = trackColor;
                for (int i = 1; i < centerlineFramesForDraw.Length; i++)
                {
                    Gizmos.DrawLine(
                        ToVector3(centerlineFramesForDraw[i - 1].Position),
                        ToVector3(centerlineFramesForDraw[i].Position));
                }
            }

            if (drawHeartline)
            {
                DrawHeartline(renderFramesForDraw);
            }

            if (drawBankingRibbon)
            {
                DrawBankingRibbon(renderFramesForDraw);
            }

            if (drawRails)
            {
                DrawRails(renderFramesForDraw);
            }

            if (drawCrossTies)
            {
                DrawCrossTies(centerlineFramesForDraw);
            }
        }

        private void DrawDebugTrackMeshPreview(IReadOnlyList<TrackFrame> frames)
        {
            if (frames == null || frames.Count < 2)
            {
                ClearDebugTrackPreviewMesh();
                return;
            }

            Mesh mesh = EnsureDebugTrackPreviewMesh();
            if (mesh == null)
            {
                return;
            }

            int frameCount = frames.Count;
            int vertexCount = frameCount * 2;
            int segmentCount = frameCount - 1;
            int indexCount = segmentCount * 6;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var indices = new int[indexCount];

            float halfGauge = trackGauge * 0.5f;
            for (int i = 0; i < frameCount; i++)
            {
                TrackFrame frame = frames[i];
                Vector3 center = ToVector3(frame.Position);

                Vector3 lateral = ToVector3(frame.Binormal);
                if (lateral.sqrMagnitude > 0.000001f)
                {
                    lateral.Normalize();
                }
                else
                {
                    lateral = Vector3.right;
                }

                Vector3 normal = ToVector3(frame.Normal);
                if (normal.sqrMagnitude > 0.000001f)
                {
                    normal.Normalize();
                }
                else
                {
                    normal = Vector3.up;
                }

                Vector3 railOffset = lateral * halfGauge;
                int vertexIndex = i * 2;
                vertices[vertexIndex] = center - railOffset;
                vertices[vertexIndex + 1] = center + railOffset;
                normals[vertexIndex] = normal;
                normals[vertexIndex + 1] = normal;

                float v = frameCount > 1
                    ? (float)i / (frameCount - 1)
                    : 0.0f;
                uvs[vertexIndex] = new Vector2(0.0f, v);
                uvs[vertexIndex + 1] = new Vector2(1.0f, v);
            }

            for (int i = 0; i < segmentCount; i++)
            {
                int baseVertex = i * 2;
                int indexOffset = i * 6;

                indices[indexOffset] = baseVertex;
                indices[indexOffset + 1] = baseVertex + 2;
                indices[indexOffset + 2] = baseVertex + 1;
                indices[indexOffset + 3] = baseVertex + 1;
                indices[indexOffset + 4] = baseVertex + 2;
                indices[indexOffset + 5] = baseVertex + 3;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateBounds();

            Color previewColor = trackColor;
            previewColor.a = Mathf.Clamp01(Mathf.Max(trackColor.a, 0.35f));
            Gizmos.color = previewColor;
            Gizmos.DrawMesh(mesh);
        }

        private Mesh EnsureDebugTrackPreviewMesh()
        {
            if (_debugTrackPreviewMesh != null)
            {
                return _debugTrackPreviewMesh;
            }

            _debugTrackPreviewMesh = new Mesh
            {
                name = "BackendTrackDebugMeshPreview"
            };
            _debugTrackPreviewMesh.hideFlags = HideFlags.HideAndDontSave;
            return _debugTrackPreviewMesh;
        }

        private void ClearDebugTrackPreviewMesh()
        {
            if (_debugTrackPreviewMesh != null)
            {
                _debugTrackPreviewMesh.Clear();
            }
        }

        private void DestroyDebugTrackPreviewMesh()
        {
            if (_debugTrackPreviewMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_debugTrackPreviewMesh);
            }
            else
            {
                DestroyImmediate(_debugTrackPreviewMesh);
            }

            _debugTrackPreviewMesh = null;
        }

        private bool TryEvaluateSmoothFramesAtDistances(
            IReadOnlyList<double> distances,
            out TrackFrame[] frames)
        {
            int controlPointSampleCount = System.Math.Max(4, smoothSplineControlPointSampleCount);
            int arcLengthSampleCount = System.Math.Max(
                smoothSplineArcLengthSampleCount,
                controlPointSampleCount * 4);
            double rollBlendDistance = System.Math.Max(0.0, smoothRollBlendDistance);

            if (TryGetCachedSmoothFrames(
                    distances,
                    controlPointSampleCount,
                    arcLengthSampleCount,
                    rollBlendDistance,
                    out frames))
            {
                return true;
            }

            try
            {
                frames = DebugTrackContinuousSampler.SampleContinuousFrames(
                    _document,
                    _evaluator,
                    distances,
                    controlPointSampleCount,
                    arcLengthSampleCount,
                    rollBlendDistance);
                CacheSmoothFrames(
                    distances,
                    frames,
                    controlPointSampleCount,
                    arcLengthSampleCount,
                    rollBlendDistance);
                return true;
            }
            catch (Exception ex)
            {
                frames = _evaluator.EvaluateFramesAtDistances(distances);
                ReportWarningOnce(
                    "Smooth continuous spline debug track sampling failed; falling back to segmented frames. " +
                    ex.Message);
                return false;
            }
        }

        private void ResetSmoothFrameSamplingCache()
        {
            _cachedSmoothControlPointSampleCount = 0;
            _cachedSmoothArcLengthSampleCount = 0;
            _cachedSmoothRollBlendDistance = double.NaN;
            _cachedSmoothFrameDistances = null;
            _cachedSmoothFrames = null;
        }

        private bool TryGetCachedSmoothFrames(
            IReadOnlyList<double> distances,
            int controlPointSampleCount,
            int arcLengthSampleCount,
            double rollBlendDistance,
            out TrackFrame[] frames)
        {
            if (_cachedSmoothFrames == null ||
                _cachedSmoothFrameDistances == null ||
                _cachedSmoothControlPointSampleCount != controlPointSampleCount ||
                _cachedSmoothArcLengthSampleCount != arcLengthSampleCount ||
                System.Math.Abs(_cachedSmoothRollBlendDistance - rollBlendDistance) > 1e-9 ||
                _cachedSmoothFrameDistances.Length != distances.Count)
            {
                frames = null;
                return false;
            }

            for (int i = 0; i < distances.Count; i++)
            {
                if (System.Math.Abs(_cachedSmoothFrameDistances[i] - distances[i]) > 1e-9)
                {
                    frames = null;
                    return false;
                }
            }

            frames = _cachedSmoothFrames;
            return true;
        }

        private void CacheSmoothFrames(
            IReadOnlyList<double> distances,
            TrackFrame[] frames,
            int controlPointSampleCount,
            int arcLengthSampleCount,
            double rollBlendDistance)
        {
            _cachedSmoothControlPointSampleCount = controlPointSampleCount;
            _cachedSmoothArcLengthSampleCount = arcLengthSampleCount;
            _cachedSmoothRollBlendDistance = rollBlendDistance;
            _cachedSmoothFrameDistances = new double[distances.Count];
            for (int i = 0; i < distances.Count; i++)
            {
                _cachedSmoothFrameDistances[i] = distances[i];
            }

            _cachedSmoothFrames = new TrackFrame[frames.Length];
            Array.Copy(frames, _cachedSmoothFrames, frames.Length);
        }

        private void CaptureSmoothnessDiagnostics(
            double totalLength,
            IReadOnlyList<TrackFrame> smoothedFrames,
            IReadOnlyList<double> smoothedDistances)
        {
            if (!drawSmoothnessDiagnosticsHud && !logSmoothnessDiagnostics)
            {
                _lastSmoothnessReport = null;
                _lastSegmentedSmoothnessReport = null;
                _lastSmoothContinuousSmoothnessReport = null;
                return;
            }

            int diagnosticSampleCount = System.Math.Max(
                smoothnessDiagnosticsSampleCount,
                centerlineSampleCount);
            double[] diagnosticDistances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                totalLength,
                diagnosticSampleCount);
            TrackFrame[] segmentedDiagnosticFrames = _evaluator.EvaluateFramesAtDistances(diagnosticDistances);
            _lastSegmentedSmoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
                segmentedDiagnosticFrames,
                diagnosticDistances);

            _lastSmoothContinuousSmoothnessReport = null;
            if (TryEvaluateSmoothFramesAtDistances(
                    diagnosticDistances,
                    out TrackFrame[] smoothDiagnosticFrames))
            {
                _lastSmoothContinuousSmoothnessReport = TrackFrameSmoothnessDiagnostics.Analyze(
                    smoothDiagnosticFrames,
                    diagnosticDistances);
            }

            _lastSmoothnessReport = debugTrackMode == DebugTrackMode.SmoothContinuousSpline &&
                                    _lastSmoothContinuousSmoothnessReport != null
                ? _lastSmoothContinuousSmoothnessReport
                : _lastSegmentedSmoothnessReport;

            if (logSmoothnessDiagnostics && !_hasLoggedSmoothnessDiagnostics)
            {
                _hasLoggedSmoothnessDiagnostics = true;
                LogSmoothnessDiagnostics(
                    _lastSmoothnessReport,
                    diagnosticSampleCount,
                    gizmoSubSamplesPerSegment,
                    smoothedFrames.Count,
                    smoothedDistances.Count,
                    debugTrackMode);
                LogSmoothnessComparisonDiagnostics(
                    _lastSegmentedSmoothnessReport,
                    _lastSmoothContinuousSmoothnessReport,
                    diagnosticSampleCount);
            }
        }

        private void DrawHeartline(IReadOnlyList<TrackFrame> frames)
        {
            if (frames == null || frames.Count < 2)
            {
                return;
            }

            Gizmos.color = heartlineColor;
            Vector3 previous = ToVector3(frames[0].Position + (frames[0].Normal * heartlineHeight));

            for (int i = 1; i < frames.Count; i++)
            {
                Vector3 current = ToVector3(frames[i].Position + (frames[i].Normal * heartlineHeight));
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private void DrawBankingRibbon(IReadOnlyList<TrackFrame> frames)
        {
            DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildBankingRibbon(
                frames,
                bankingRibbonHalfWidth,
                bankingRibbonNormalOffset);

            for (int i = 0; i < segments.Length; i++)
            {
                Gizmos.color = segments[i].AxisType == TrackFrameAxisType.Normal
                    ? normalColor
                    : bankingRibbonColor;

                Gizmos.DrawLine(ToVector3(segments[i].Start), ToVector3(segments[i].End));
            }
        }

        private void DrawRails(IReadOnlyList<TrackFrame> frames)
        {
            if (frames == null || frames.Count < 2)
            {
                return;
            }

            float halfGauge = trackGauge * 0.5f;
            GetRailPositions(frames[0], halfGauge, out Vector3 previousLeftRail, out Vector3 previousRightRail);

            Gizmos.color = trackColor;
            for (int i = 1; i < frames.Count; i++)
            {
                GetRailPositions(frames[i], halfGauge, out Vector3 currentLeftRail, out Vector3 currentRightRail);
                Gizmos.DrawLine(previousLeftRail, currentLeftRail);
                Gizmos.DrawLine(previousRightRail, currentRightRail);
                previousLeftRail = currentLeftRail;
                previousRightRail = currentRightRail;
            }
        }

        private void DrawCrossTies(IReadOnlyList<TrackFrame> frames)
        {
            DebugLineSegment[] ties = TrackFrameDebugGizmoBuilder.BuildRailCrossTies(
                frames,
                trackGauge,
                railCrossTieSpacing);

            Gizmos.color = crossTieColor;
            for (int i = 0; i < ties.Length; i++)
            {
                Gizmos.DrawLine(ToVector3(ties[i].Start), ToVector3(ties[i].End));
            }
        }

        private static void GetRailPositions(TrackFrame frame, float halfGauge, out Vector3 leftRail, out Vector3 rightRail)
        {
            Vector3 center = ToVector3(frame.Position);
            Vector3 lateral = ToVector3(frame.Binormal);
            if (lateral.sqrMagnitude > 0.000001f)
            {
                lateral.Normalize();
            }
            else
            {
                lateral = Vector3.right;
            }

            Vector3 railOffset = lateral * halfGauge;
            leftRail = center - railOffset;
            rightRail = center + railOffset;
        }

        private double[] GetCenterlineDistances(double totalLength)
        {
            if (_cachedCenterlineDistances != null &&
                _cachedDistanceSampleCount == centerlineSampleCount &&
                Math.Abs(_cachedDistanceTotalLength - totalLength) <= 1e-9)
            {
                return _cachedCenterlineDistances;
            }

            double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                totalLength,
                centerlineSampleCount);

            _cachedCenterlineDistances = distances;
            _cachedDistanceSampleCount = centerlineSampleCount;
            _cachedDistanceTotalLength = totalLength;
            return distances;
        }

        private double[] GetGizmoSubsampleDistances(double totalLength)
        {
            if (_cachedGizmoDistances != null &&
                _cachedGizmoDistanceSampleCount == centerlineSampleCount &&
                _cachedGizmoSubSamplesPerSegment == gizmoSubSamplesPerSegment &&
                Math.Abs(_cachedGizmoDistanceTotalLength - totalLength) <= 1e-9)
            {
                return _cachedGizmoDistances;
            }

            double[] distances = TrackFrameDebugGizmoBuilder.BuildUniformFrameDistances(
                totalLength,
                centerlineSampleCount,
                gizmoSubSamplesPerSegment);

            _cachedGizmoDistances = distances;
            _cachedGizmoDistanceSampleCount = centerlineSampleCount;
            _cachedGizmoSubSamplesPerSegment = gizmoSubSamplesPerSegment;
            _cachedGizmoDistanceTotalLength = totalLength;
            return distances;
        }

        private void DrawCarWireBoxes(IEnumerable<TrainCarTransform> cars)
        {
            DebugLineSegment[] segments = TrainCarDebugGizmoBuilder.BuildWireBoxes(cars, carLength, carWidth, carHeight);
            DrawDebugLineSegmentsByAxis(segments);
        }

        private void DrawCarAxes(IReadOnlyList<TrainCarTransform> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                DebugLineSegment[] segments = TrackFrameDebugGizmoBuilder.BuildAxes(cars[i].Frame, axisLength);
                DrawDebugLineSegmentsByAxis(segments);
            }
        }

        private void DrawSpacingLinks(IReadOnlyList<TrainCarTransform> cars)
        {
            for (int i = 1; i < cars.Count; i++)
            {
                TrainCarTransform front = cars[i - 1];
                TrainCarTransform rear = cars[i];

                double actualSpacing = front.Distance - rear.Distance;
                double spacingError = Math.Abs(actualSpacing - carSpacing);

                Gizmos.color = spacingError <= spacingTolerance ? spacingColorGood : spacingColorWarning;

                Vector3 a = ToVector3(front.Frame.Position);
                Vector3 b = ToVector3(rear.Frame.Position);
                Gizmos.DrawLine(a, b);

                if (spacingDotRadius > 0.0f)
                {
                    Gizmos.DrawSphere((a + b) * 0.5f, spacingDotRadius);
                }
            }
        }

        private void DrawBogieMarkers(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                DrawMarker(cars[i].FrontBogie.Bogie.Frame.Position, bogieMarkerColor);
                DrawMarker(cars[i].RearBogie.Bogie.Frame.Position, bogieMarkerColor);
            }
        }

        private void DrawWheelMarkers(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                DrawWheelMarkersForBogie(cars[i].FrontBogie);
                DrawWheelMarkersForBogie(cars[i].RearBogie);
            }
        }

        private void DrawWheelMarkersForBogie(TrainBogieWithWheelsTransform bogieWithWheels)
        {
            IReadOnlyList<WheelTransform> wheels = bogieWithWheels.WheelsReadOnly;
            for (int i = 0; i < wheels.Count; i++)
            {
                DrawMarker(GetWheelMarkerPosition(wheels[i]), wheelMarkerColor);
            }
        }

        private void DrawArticulationCenters(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                DrawMarker(cars[i].Body.ArticulatedFrame.Position, articulationMarkerColor);
            }
        }

        private void DrawCouplerConnections(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars)
        {
            Gizmos.color = couplerColor;
            for (int i = 1; i < cars.Count; i++)
            {
                Vector3 previousRear = ToVector3(cars[i - 1].Body.RearBogie.Frame.Position);
                Vector3 currentFront = ToVector3(cars[i].Body.FrontBogie.Frame.Position);
                Gizmos.DrawLine(previousRear, currentFront);
            }
        }

        private void DrawMarker(Vector3d position, Color color)
        {
            Gizmos.color = color;
            if (detailMarkerRadius <= 0.0f)
            {
                return;
            }

            Gizmos.DrawSphere(ToVector3(position), detailMarkerRadius);
        }

        private static Vector3d GetWheelMarkerPosition(WheelTransform wheel)
        {
            TrackFrame frame = wheel.Frame;
            return frame.Position
                   + (frame.Tangent * wheel.LocalOffsetX)
                   + (frame.Normal * wheel.LocalOffsetY)
                   + (frame.Binormal * wheel.LocalOffsetZ);
        }

        private TrainConsistDefinition BuildDerivedConsistDefinition()
        {
            double derivedBogieSpacing = Clamp(carLength * 0.55f, 0.01, carLength);
            double derivedWheelRadius = System.Math.Max(0.01, carHeight * 0.2f);
            double derivedWheelWidth = System.Math.Max(0.01, carWidth * 0.2f);
            double derivedAxleSpacing = System.Math.Max(0.0, derivedBogieSpacing * 0.5);

            var wheelLayout = new TrainWheelLayout(
                wheelCountPerBogie: 2,
                wheelRadius: derivedWheelRadius,
                wheelWidth: derivedWheelWidth,
                axleSpacing: derivedAxleSpacing);

            return new TrainConsistDefinition(
                carCount: carCount,
                carSpacing: carSpacing,
                carLength: carLength,
                carWidth: carWidth,
                carHeight: carHeight,
                bogieSpacing: derivedBogieSpacing,
                wheelLayout: wheelLayout);
        }

        private static IReadOnlyList<TrainCarTransform> ExtractBodyTransforms(IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> articulatedCars)
        {
            var bodies = new List<TrainCarTransform>(articulatedCars.Count);
            for (int i = 0; i < articulatedCars.Count; i++)
            {
                bodies.Add(articulatedCars[i].Body.OriginalBody);
            }

            return bodies;
        }

        private bool TryBuildSmoothDebugFrameTrainPlacement(
            IReadOnlyList<TrainCarTransform> segmentedCars,
            out IReadOnlyList<TrainCarTransform> smoothCars)
        {
            if (segmentedCars == null)
            {
                smoothCars = null;
                return false;
            }

            if (segmentedCars.Count == 0)
            {
                smoothCars = Array.Empty<TrainCarTransform>();
                return true;
            }

            var carDistances = new double[segmentedCars.Count];
            for (int i = 0; i < segmentedCars.Count; i++)
            {
                carDistances[i] = segmentedCars[i].Distance;
            }

            if (!TryEvaluateSmoothFramesAtDistances(carDistances, out TrackFrame[] smoothFrames))
            {
                smoothCars = null;
                return false;
            }

            var remappedCars = new TrainCarTransform[segmentedCars.Count];
            for (int i = 0; i < segmentedCars.Count; i++)
            {
                TrainCarTransform segmentedCar = segmentedCars[i];
                TrackFrame smoothFrame = smoothFrames[i];
                remappedCars[i] = new TrainCarTransform(
                    segmentedCar.CarIndex,
                    segmentedCar.Distance,
                    smoothFrame,
                    smoothFrame.ToMatrix4x4());
            }

            smoothCars = remappedCars;
            return true;
        }

        private bool AdvancePlayhead(float deltaTime)
        {
            if (!autoPlay || playbackSpeed <= 0.0f || deltaTime <= 0.0f)
            {
                return false;
            }

            float previous = playhead01;
            float delta01 = playbackSpeed * deltaTime;
            float candidate = previous + delta01;
            playhead01 = loopPlayback
                ? Mathf.Repeat(candidate, 1.0f)
                : Mathf.Clamp01(candidate);

            return Mathf.Abs(playhead01 - previous) > 0.000001f;
        }

        private void DrawDebugHudLabel(
            IReadOnlyList<TrainCarTransform> cars,
            double totalLength,
            double requestedLeadDistance,
            double leadDistance)
        {
#if UNITY_EDITOR
            if (!drawDebugHud || cars == null || cars.Count == 0)
            {
                return;
            }

            (double maxSpacingError, double averageSpacingError) = GetSpacingErrorStats(cars);
            string sampledSpeedText = _hasSampledLeadSpeed
                ? $"{_lastSampledLeadSpeed:F3} u/s"
                : "n/a";
            string leadDistanceText = clampLeadDistance && System.Math.Abs(leadDistance - requestedLeadDistance) > 1e-9
                ? $"{leadDistance:F3} (requested {requestedLeadDistance:F3})"
                : $"{leadDistance:F3}";

            string label =
                $"debug track mode: {debugTrackMode}\n" +
                $"train placement mode: {(useSmoothDebugFramesForTrainPlacement ? "smooth-debug-frame (experimental)" : "segmented")}\n" +
                $"auto-play: {(autoPlay ? "on" : "off")} speed01/s: {playbackSpeed:F3} loop: {loopPlayback}\n" +
                $"playhead01: {playhead01:F3}\n" +
                $"lead distance: {leadDistanceText} / {totalLength:F3}\n" +
                $"spacing error max/avg: {maxSpacingError:F4} / {averageSpacingError:F4}\n" +
                $"sampled speed est: {sampledSpeedText}";

            if (drawSmoothnessDiagnosticsHud && _lastSmoothnessReport != null)
            {
                label += "\n" +
                    $"frame delta deg max/avg: {_lastSmoothnessReport.FrameAngleDelta.MaxAbsoluteDegrees:F3} / {_lastSmoothnessReport.FrameAngleDelta.AverageAbsoluteDegrees:F3}\n" +
                    $"t/n/b deg max: {_lastSmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees:F3} / {_lastSmoothnessReport.NormalAngleDelta.MaxAbsoluteDegrees:F3} / {_lastSmoothnessReport.BinormalAngleDelta.MaxAbsoluteDegrees:F3}\n" +
                    $"twist deg max/avg: {_lastSmoothnessReport.FrameTwistDelta.MaxAbsoluteDegrees:F3} / {_lastSmoothnessReport.FrameTwistDelta.AverageAbsoluteDegrees:F3}\n" +
                    $"|dCurv| max/avg: {_lastSmoothnessReport.CurvatureEstimateDelta.MaxAbsolute:F5} / {_lastSmoothnessReport.CurvatureEstimateDelta.AverageAbsolute:F5}";

                if (_lastSegmentedSmoothnessReport != null && _lastSmoothContinuousSmoothnessReport != null)
                {
                    label += "\n" +
                        $"seg->smooth frame max deg: {_lastSegmentedSmoothnessReport.FrameAngleDelta.MaxAbsoluteDegrees:F3} -> {_lastSmoothContinuousSmoothnessReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}\n" +
                        $"seg->smooth tangent max deg: {_lastSegmentedSmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees:F3} -> {_lastSmoothContinuousSmoothnessReport.TangentAngleDelta.MaxAbsoluteDegrees:F3}\n" +
                        $"seg->smooth twist max deg: {_lastSegmentedSmoothnessReport.FrameTwistDelta.MaxAbsoluteDegrees:F3} -> {_lastSmoothContinuousSmoothnessReport.FrameTwistDelta.MaxAbsoluteDegrees:F3}\n" +
                        $"seg->smooth |dCurv| max: {_lastSegmentedSmoothnessReport.CurvatureEstimateDelta.MaxAbsolute:F5} -> {_lastSmoothContinuousSmoothnessReport.CurvatureEstimateDelta.MaxAbsolute:F5}";
                }
            }

            Vector3 anchor = ToVector3(cars[0].Frame.Position);
            Vector3 normal = ToVector3(cars[0].Frame.Normal);
            if (normal.sqrMagnitude <= 0.000001f)
            {
                normal = Vector3.up;
            }
            else
            {
                normal.Normalize();
            }

            Handles.Label(anchor + (normal * debugHudVerticalOffset), label);
#endif
        }

        private (double maxSpacingError, double averageSpacingError) GetSpacingErrorStats(IReadOnlyList<TrainCarTransform> cars)
        {
            if (cars == null || cars.Count < 2)
            {
                return (0.0, 0.0);
            }

            double maxSpacingError = 0.0;
            double sumSpacingError = 0.0;
            int spacingLinkCount = 0;

            for (int i = 1; i < cars.Count; i++)
            {
                double actualSpacing = cars[i - 1].Distance - cars[i].Distance;
                double spacingError = System.Math.Abs(actualSpacing - carSpacing);
                maxSpacingError = System.Math.Max(maxSpacingError, spacingError);
                sumSpacingError += spacingError;
                spacingLinkCount++;
            }

            double averageSpacingError = spacingLinkCount > 0
                ? sumSpacingError / spacingLinkCount
                : 0.0;
            return (maxSpacingError, averageSpacingError);
        }

        private void UpdateSampledLeadSpeed(double leadDistance)
        {
            double now = GetCurrentTimeSeconds();
            if (double.IsNaN(_lastLeadDistanceSampleTime))
            {
                _lastLeadDistanceSampleTime = now;
                _lastLeadDistanceForSpeed = leadDistance;
                _hasSampledLeadSpeed = false;
                return;
            }

            double deltaTime = now - _lastLeadDistanceSampleTime;
            if (deltaTime <= 1e-6)
            {
                return;
            }

            _lastSampledLeadSpeed = (leadDistance - _lastLeadDistanceForSpeed) / deltaTime;
            _lastLeadDistanceForSpeed = leadDistance;
            _lastLeadDistanceSampleTime = now;
            _hasSampledLeadSpeed = true;
        }

        private void CaptureTrainSamplingDiagnostics(
            double leadDistance,
            IReadOnlyList<TrainCarTransform> segmentedCars,
            IReadOnlyList<TrainCarTransform> smoothDebugCars,
            IReadOnlyList<TrainCarTransform> activeCars,
            bool usingSmoothTrainPlacement,
            string trainBodyFramePathLabel,
            IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> articulatedCars)
        {
            if (!logTrainSamplingDiagnostics ||
                segmentedCars == null ||
                segmentedCars.Count == 0 ||
                activeCars == null ||
                activeCars.Count == 0)
            {
                ResetTrainSamplingDiagnosticsState();
                return;
            }

            bool smoothComparisonAvailable = smoothDebugCars != null &&
                                             smoothDebugCars.Count == segmentedCars.Count;
            double now = GetCurrentTimeSeconds();
            TrainMotionDeltaStats segmentedMotionDelta = BuildTrainMotionDeltaStats(
                segmentedCars,
                leadDistance,
                now,
                _segmentedTrainSamplingDiagnosticsState);
            UpdateTrainSamplingDiagnosticsSnapshot(
                segmentedCars,
                leadDistance,
                now,
                ref _segmentedTrainSamplingDiagnosticsState);

            TrainMotionDeltaStats smoothMotionDelta = default;
            if (smoothComparisonAvailable)
            {
                smoothMotionDelta = BuildTrainMotionDeltaStats(
                    smoothDebugCars,
                    leadDistance,
                    now,
                    _smoothTrainSamplingDiagnosticsState);
                UpdateTrainSamplingDiagnosticsSnapshot(
                    smoothDebugCars,
                    leadDistance,
                    now,
                    ref _smoothTrainSamplingDiagnosticsState);
            }
            else
            {
                ResetTrainSamplingDiagnosticsState(ref _smoothTrainSamplingDiagnosticsState);
            }

            TrainMotionDeltaStats activeMotionDelta = usingSmoothTrainPlacement && smoothComparisonAvailable
                ? smoothMotionDelta
                : segmentedMotionDelta;

            if (!ShouldLogTrainSamplingDiagnostics(now))
            {
                return;
            }

            var trackSampleDistances = new List<double>(1 + ((articulatedCars?.Count ?? 0) * 2))
            {
                leadDistance
            };

            if (articulatedCars != null)
            {
                for (int i = 0; i < articulatedCars.Count; i++)
                {
                    trackSampleDistances.Add(articulatedCars[i].FrontBogie.Bogie.Distance);
                    trackSampleDistances.Add(articulatedCars[i].RearBogie.Bogie.Distance);
                }
            }

            TrackFrame[] activeTrackFrames = EvaluateFramesForActiveDebugPath(trackSampleDistances, out string activeFramePathLabel);
            TrackFrame leadTrackFrame = activeTrackFrames[0];
            TrackFrame leadBodyFrame = activeCars[0].Frame;
            FrameDeltaMetrics leadFrameDelta = BuildFrameDelta(leadTrackFrame, leadBodyFrame);

            int bogieCount = 0;
            int bogiePairCount = 0;
            double maxBogieTrackFrameDelta = 0.0;
            double sumBogieTrackFrameDelta = 0.0;
            double maxBogieTrackPositionDelta = 0.0;
            double sumBogieTrackPositionDelta = 0.0;
            double maxBogiePairFrameDelta = 0.0;
            double sumBogiePairFrameDelta = 0.0;
            double maxBogiePairFrameDeltaPerMeter = 0.0;
            bool hasBogieSamples = false;

            if (articulatedCars != null && articulatedCars.Count > 0)
            {
                hasBogieSamples = true;
                int sampleCursor = 1;

                for (int i = 0; i < articulatedCars.Count; i++)
                {
                    BogieTransform frontBogie = articulatedCars[i].FrontBogie.Bogie;
                    BogieTransform rearBogie = articulatedCars[i].RearBogie.Bogie;

                    FrameDeltaMetrics frontTrackDelta = BuildFrameDelta(activeTrackFrames[sampleCursor], frontBogie.Frame);
                    sampleCursor++;
                    FrameDeltaMetrics rearTrackDelta = BuildFrameDelta(activeTrackFrames[sampleCursor], rearBogie.Frame);
                    sampleCursor++;

                    maxBogieTrackFrameDelta = System.Math.Max(maxBogieTrackFrameDelta, frontTrackDelta.MaxAxisDeltaDegrees);
                    maxBogieTrackFrameDelta = System.Math.Max(maxBogieTrackFrameDelta, rearTrackDelta.MaxAxisDeltaDegrees);
                    sumBogieTrackFrameDelta += frontTrackDelta.MaxAxisDeltaDegrees + rearTrackDelta.MaxAxisDeltaDegrees;

                    maxBogieTrackPositionDelta = System.Math.Max(maxBogieTrackPositionDelta, frontTrackDelta.PositionDelta);
                    maxBogieTrackPositionDelta = System.Math.Max(maxBogieTrackPositionDelta, rearTrackDelta.PositionDelta);
                    sumBogieTrackPositionDelta += frontTrackDelta.PositionDelta + rearTrackDelta.PositionDelta;

                    FrameDeltaMetrics pairDelta = BuildFrameDelta(frontBogie.Frame, rearBogie.Frame);
                    double bogieSeparation = System.Math.Abs(frontBogie.Distance - rearBogie.Distance);
                    double pairDeltaPerMeter = bogieSeparation > 1e-9
                        ? pairDelta.MaxAxisDeltaDegrees / bogieSeparation
                        : 0.0;

                    maxBogiePairFrameDelta = System.Math.Max(maxBogiePairFrameDelta, pairDelta.MaxAxisDeltaDegrees);
                    sumBogiePairFrameDelta += pairDelta.MaxAxisDeltaDegrees;
                    maxBogiePairFrameDeltaPerMeter = System.Math.Max(maxBogiePairFrameDeltaPerMeter, pairDeltaPerMeter);

                    bogieCount += 2;
                    bogiePairCount++;
                }
            }

            double averageBogieTrackFrameDelta = bogieCount > 0
                ? sumBogieTrackFrameDelta / bogieCount
                : 0.0;
            double averageBogieTrackPositionDelta = bogieCount > 0
                ? sumBogieTrackPositionDelta / bogieCount
                : 0.0;
            double averageBogiePairFrameDelta = bogiePairCount > 0
                ? sumBogiePairFrameDelta / bogiePairCount
                : 0.0;

            var message = new StringBuilder(768);
            message.Append("BackendTrainPipelineGizmoVisualizer train diagnostics: ");
            message.Append($"debugTrackMode={debugTrackMode}, activeTrackFramePath={activeFramePathLabel}, ");
            message.Append($"trainBodyFramePath={trainBodyFramePathLabel}, ");
            message.Append($"leadDistance={leadDistance:F3}, ");
            message.Append($"leadTrackVsBody frameMaxDeg={leadFrameDelta.MaxAxisDeltaDegrees:F4} ");
            message.Append($"(t/n/b={leadFrameDelta.TangentDeltaDegrees:F4}/{leadFrameDelta.NormalDeltaDegrees:F4}/{leadFrameDelta.BinormalDeltaDegrees:F4}), ");
            message.Append($"leadTrackVsBody posDelta={leadFrameDelta.PositionDelta:F5}.");

            if (hasBogieSamples)
            {
                message.Append(" ");
                message.Append($"bogieTrackMismatch frameMax/avgDeg={maxBogieTrackFrameDelta:F4}/{averageBogieTrackFrameDelta:F4}, ");
                message.Append($"bogieTrackMismatch posMax/avg={maxBogieTrackPositionDelta:F5}/{averageBogieTrackPositionDelta:F5}, ");
                message.Append($"frontRearBogie frameMax/avgDeg={maxBogiePairFrameDelta:F4}/{averageBogiePairFrameDelta:F4}, ");
                message.Append($"frontRearBogie frameMaxDegPerMeter={maxBogiePairFrameDeltaPerMeter:F4}.");
            }
            else
            {
                message.Append(" bogie diagnostics unavailable (detail pose sampling is disabled).");
            }

            if (activeMotionDelta.HasPreviousSample)
            {
                double leadStepPerSecond = activeMotionDelta.DeltaTimeSeconds > 1e-9
                    ? activeMotionDelta.LeadDistanceDelta / activeMotionDelta.DeltaTimeSeconds
                    : 0.0;

                message.Append(" ");
                message.Append(
                    $"updateDelta dt={activeMotionDelta.DeltaTimeSeconds:F4}s, leadDelta={activeMotionDelta.LeadDistanceDelta:F5}, leadDeltaPerSec={leadStepPerSecond:F5}, ");
                message.Append(
                    $"carTransformDelta posMax/avg={activeMotionDelta.MaxCarPositionDelta:F5}/{activeMotionDelta.AverageCarPositionDelta:F5}, ");
                message.Append(
                    $"carTransformDelta frameMax/avgDeg={activeMotionDelta.MaxCarFrameDeltaDegrees:F4}/{activeMotionDelta.AverageCarFrameDeltaDegrees:F4}, ");
                message.Append(
                    $"comparedCars={activeMotionDelta.ComparedCars} (prevCount={activeMotionDelta.PreviousCarCount}, currentCount={activeCars.Count}).");
            }
            else
            {
                message.Append(" updateDelta unavailable for first sample.");
            }

            if (smoothComparisonAvailable)
            {
                AppendTrainJoltComparisonDiagnostics(
                    message,
                    segmentedMotionDelta,
                    smoothMotionDelta);
            }
            else
            {
                message.Append(" smooth debug-frame jolt comparison unavailable (smooth sampling fallback or disabled).");
            }

            Debug.Log(message.ToString(), this);
            _lastTrainSamplingDiagnosticsLogTime = now;
        }

        private TrackFrame[] EvaluateFramesForActiveDebugPath(IReadOnlyList<double> distances, out string pathLabel)
        {
            if (debugTrackMode == DebugTrackMode.SmoothContinuousSpline)
            {
                bool smoothSucceeded = TryEvaluateSmoothFramesAtDistances(distances, out TrackFrame[] smoothFrames);
                pathLabel = smoothSucceeded
                    ? "SmoothContinuousSpline"
                    : "SmoothContinuousSpline(fallback->SegmentedCurrent)";
                return smoothFrames;
            }

            pathLabel = "SegmentedCurrent";
            return _evaluator.EvaluateFramesAtDistances(distances);
        }

        private TrainMotionDeltaStats BuildTrainMotionDeltaStats(
            IReadOnlyList<TrainCarTransform> cars,
            double leadDistance,
            double sampleTime,
            TrainSamplingDiagnosticsState previousState)
        {
            if (double.IsNaN(previousState.LastSampleTime) ||
                previousState.LastCarSnapshots == null ||
                previousState.LastCarSnapshots.Length == 0)
            {
                return new TrainMotionDeltaStats(
                    hasPreviousSample: false,
                    deltaTimeSeconds: 0.0,
                    leadDistanceDelta: 0.0,
                    comparedCars: 0,
                    previousCarCount: previousState.LastCarSnapshots?.Length ?? 0,
                    maxCarPositionDelta: 0.0,
                    averageCarPositionDelta: 0.0,
                    maxCarFrameDeltaDegrees: 0.0,
                    averageCarFrameDeltaDegrees: 0.0);
            }

            int comparedCars = System.Math.Min(cars.Count, previousState.LastCarSnapshots.Length);
            if (comparedCars == 0)
            {
                return new TrainMotionDeltaStats(
                    hasPreviousSample: true,
                    deltaTimeSeconds: sampleTime - previousState.LastSampleTime,
                    leadDistanceDelta: leadDistance - previousState.LastLeadDistance,
                    comparedCars: 0,
                    previousCarCount: previousState.LastCarSnapshots.Length,
                    maxCarPositionDelta: 0.0,
                    averageCarPositionDelta: 0.0,
                    maxCarFrameDeltaDegrees: 0.0,
                    averageCarFrameDeltaDegrees: 0.0);
            }

            double maxCarPositionDelta = 0.0;
            double sumCarPositionDelta = 0.0;
            double maxCarFrameDeltaDegrees = 0.0;
            double sumCarFrameDeltaDegrees = 0.0;

            for (int i = 0; i < comparedCars; i++)
            {
                TrainFrameSnapshot previousSnapshot = previousState.LastCarSnapshots[i];
                TrainCarTransform currentCar = cars[i];
                FrameDeltaMetrics frameDelta = BuildFrameDelta(previousSnapshot.Frame, currentCar.Frame);

                maxCarPositionDelta = System.Math.Max(maxCarPositionDelta, frameDelta.PositionDelta);
                sumCarPositionDelta += frameDelta.PositionDelta;

                maxCarFrameDeltaDegrees = System.Math.Max(maxCarFrameDeltaDegrees, frameDelta.MaxAxisDeltaDegrees);
                sumCarFrameDeltaDegrees += frameDelta.MaxAxisDeltaDegrees;
            }

            double averageCarPositionDelta = sumCarPositionDelta / comparedCars;
            double averageCarFrameDeltaDegrees = sumCarFrameDeltaDegrees / comparedCars;

            return new TrainMotionDeltaStats(
                hasPreviousSample: true,
                deltaTimeSeconds: sampleTime - previousState.LastSampleTime,
                leadDistanceDelta: leadDistance - previousState.LastLeadDistance,
                comparedCars: comparedCars,
                previousCarCount: previousState.LastCarSnapshots.Length,
                maxCarPositionDelta: maxCarPositionDelta,
                averageCarPositionDelta: averageCarPositionDelta,
                maxCarFrameDeltaDegrees: maxCarFrameDeltaDegrees,
                averageCarFrameDeltaDegrees: averageCarFrameDeltaDegrees);
        }

        private void UpdateTrainSamplingDiagnosticsSnapshot(
            IReadOnlyList<TrainCarTransform> cars,
            double leadDistance,
            double sampleTime,
            ref TrainSamplingDiagnosticsState state)
        {
            var snapshots = new TrainFrameSnapshot[cars.Count];
            for (int i = 0; i < cars.Count; i++)
            {
                TrainCarTransform car = cars[i];
                snapshots[i] = new TrainFrameSnapshot(car.CarIndex, car.Distance, car.Frame);
            }

            state.LastCarSnapshots = snapshots;
            state.LastLeadDistance = leadDistance;
            state.LastSampleTime = sampleTime;
        }

        private bool ShouldLogTrainSamplingDiagnostics(double sampleTime)
        {
            if (double.IsNaN(_lastTrainSamplingDiagnosticsLogTime))
            {
                return true;
            }

            double intervalSeconds = System.Math.Max(0.0, trainSamplingDiagnosticsIntervalSeconds);
            if (intervalSeconds <= 0.0)
            {
                return true;
            }

            return (sampleTime - _lastTrainSamplingDiagnosticsLogTime) >= intervalSeconds;
        }

        private void ResetTrainSamplingDiagnosticsState()
        {
            _lastTrainSamplingDiagnosticsLogTime = double.NaN;
            ResetTrainSamplingDiagnosticsState(ref _segmentedTrainSamplingDiagnosticsState);
            ResetTrainSamplingDiagnosticsState(ref _smoothTrainSamplingDiagnosticsState);
        }

        private static void ResetTrainSamplingDiagnosticsState(ref TrainSamplingDiagnosticsState state)
        {
            state.LastSampleTime = double.NaN;
            state.LastLeadDistance = double.NaN;
            state.LastCarSnapshots = null;
        }

        private static void AppendTrainJoltComparisonDiagnostics(
            StringBuilder message,
            TrainMotionDeltaStats segmentedMotionDelta,
            TrainMotionDeltaStats smoothMotionDelta)
        {
            if (!segmentedMotionDelta.HasPreviousSample ||
                !smoothMotionDelta.HasPreviousSample ||
                segmentedMotionDelta.ComparedCars <= 0 ||
                smoothMotionDelta.ComparedCars <= 0)
            {
                message.Append(" joltComparison pending previous samples for both segmented and smooth placement.");
                return;
            }

            bool frameJoltReduced = smoothMotionDelta.MaxCarFrameDeltaDegrees < segmentedMotionDelta.MaxCarFrameDeltaDegrees;
            bool positionJoltReduced = smoothMotionDelta.MaxCarPositionDelta < segmentedMotionDelta.MaxCarPositionDelta;
            bool overallJoltReduced = frameJoltReduced && positionJoltReduced;

            double frameChangePercent = ComputeRelativeChangePercent(
                segmentedMotionDelta.MaxCarFrameDeltaDegrees,
                smoothMotionDelta.MaxCarFrameDeltaDegrees);
            double positionChangePercent = ComputeRelativeChangePercent(
                segmentedMotionDelta.MaxCarPositionDelta,
                smoothMotionDelta.MaxCarPositionDelta);

            message.Append(" ");
            message.Append(
                $"joltComparison segmented(posMax/avg={segmentedMotionDelta.MaxCarPositionDelta:F5}/{segmentedMotionDelta.AverageCarPositionDelta:F5}, frameMax/avgDeg={segmentedMotionDelta.MaxCarFrameDeltaDegrees:F4}/{segmentedMotionDelta.AverageCarFrameDeltaDegrees:F4}) ");
            message.Append(
                $"vs smoothDebug(posMax/avg={smoothMotionDelta.MaxCarPositionDelta:F5}/{smoothMotionDelta.AverageCarPositionDelta:F5}, frameMax/avgDeg={smoothMotionDelta.MaxCarFrameDeltaDegrees:F4}/{smoothMotionDelta.AverageCarFrameDeltaDegrees:F4}); ");
            message.Append(
                $"smooth-debug-frame jolts reduced={(overallJoltReduced ? "yes" : "no")} ");
            message.Append(
                $"(frame {FormatPercentChange(frameChangePercent)}, position {FormatPercentChange(positionChangePercent)}).");
        }

        private static double ComputeRelativeChangePercent(double baseline, double comparison)
        {
            if (baseline <= 1e-9)
            {
                if (comparison <= 1e-9)
                {
                    return 0.0;
                }

                return double.NaN;
            }

            return ((comparison - baseline) / baseline) * 100.0;
        }

        private static string FormatPercentChange(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "n/a";
            }

            return $"{value:+0.0;-0.0;0.0}%";
        }

        private static FrameDeltaMetrics BuildFrameDelta(TrackFrame referenceFrame, TrackFrame sampleFrame)
        {
            double positionDelta = (sampleFrame.Position - referenceFrame.Position).Length;
            double tangentDelta = ComputeVectorAngleDegrees(referenceFrame.Tangent, sampleFrame.Tangent);
            double normalDelta = ComputeVectorAngleDegrees(referenceFrame.Normal, sampleFrame.Normal);
            double binormalDelta = ComputeVectorAngleDegrees(referenceFrame.Binormal, sampleFrame.Binormal);
            return new FrameDeltaMetrics(positionDelta, tangentDelta, normalDelta, binormalDelta);
        }

        private static double ComputeVectorAngleDegrees(Vector3d a, Vector3d b)
        {
            Vector3d normalizedA = SafeNormalize(a);
            Vector3d normalizedB = SafeNormalize(b);

            if (normalizedA.LengthSquared <= 1e-12 || normalizedB.LengthSquared <= 1e-12)
            {
                return 0.0;
            }

            double dot = MathUtil.Clamp(Vector3d.Dot(normalizedA, normalizedB), -1.0, 1.0);
            return System.Math.Acos(dot) * (180.0 / System.Math.PI);
        }

        private static Vector3d SafeNormalize(Vector3d v)
        {
            if (double.IsNaN(v.X) || double.IsNaN(v.Y) || double.IsNaN(v.Z) ||
                double.IsInfinity(v.X) || double.IsInfinity(v.Y) || double.IsInfinity(v.Z))
            {
                return Vector3d.Zero;
            }

            double length = v.Length;
            if (length <= 1e-9)
            {
                return Vector3d.Zero;
            }

            return v / length;
        }

        private void ResetPlaybackSamplingState()
        {
            _lastRuntimeUpdateTime = float.NaN;
            _lastLeadDistanceForSpeed = double.NaN;
            _lastLeadDistanceSampleTime = double.NaN;
            _lastSampledLeadSpeed = 0.0;
            _hasSampledLeadSpeed = false;
            ResetTrainSamplingDiagnosticsState();
#if UNITY_EDITOR
            _lastEditorUpdateTime = double.NaN;
#endif
        }

        private static double GetCurrentTimeSeconds()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return EditorApplication.timeSinceStartup;
            }
#endif

            return Time.realtimeSinceStartup;
        }

        private void DrawDebugLineSegmentsByAxis(IEnumerable<DebugLineSegment> segments)
        {
            foreach (DebugLineSegment segment in segments)
            {
                switch (segment.AxisType)
                {
                    case TrackFrameAxisType.Tangent:
                        Gizmos.color = tangentColor;
                        break;

                    case TrackFrameAxisType.Normal:
                        Gizmos.color = normalColor;
                        break;

                    case TrackFrameAxisType.Binormal:
                        Gizmos.color = binormalColor;
                        break;

                    default:
                        Gizmos.color = Color.white;
                        break;
                }

                Gizmos.DrawLine(ToVector3(segment.Start), ToVector3(segment.End));
            }
        }

        private void ReportWarningOnce(string message)
        {
            if (string.Equals(_lastError, message, StringComparison.Ordinal))
            {
                return;
            }

            _lastError = message;
            Debug.LogWarning("BackendTrainPipelineGizmoVisualizer: " + message, this);
        }

        private void LogSmoothnessDiagnostics(
            TrackFrameSmoothnessReport report,
            int diagnosticSampleCount,
            int renderedSubSamplesPerSegment,
            int renderedFrameCount,
            int renderedDistanceCount,
            DebugTrackMode activeMode)
        {
            if (report == null || report.IntervalCount == 0)
            {
                Debug.Log(
                    "BackendTrainPipelineGizmoVisualizer smoothness: no intervals available for diagnostics.",
                    this);
                return;
            }

            TrackFrameSmoothnessInterval worstInterval = report.Intervals[0];
            for (int i = 1; i < report.Intervals.Count; i++)
            {
                TrackFrameSmoothnessInterval candidate = report.Intervals[i];
                if (candidate.FrameAngleDeltaRadians > worstInterval.FrameAngleDeltaRadians)
                {
                    worstInterval = candidate;
                }
            }

            string message =
                "BackendTrainPipelineGizmoVisualizer smoothness diagnostics " +
                $"(mode={activeMode}, diagnosticSamples={diagnosticSampleCount}, renderedFrames={renderedFrameCount}, renderedDistances={renderedDistanceCount}, gizmoSubSamplesPerSegment={renderedSubSamplesPerSegment}): " +
                $"frame deg max/avg={report.FrameAngleDelta.MaxAbsoluteDegrees:F3}/{report.FrameAngleDelta.AverageAbsoluteDegrees:F3}, " +
                $"tangent deg max/avg={report.TangentAngleDelta.MaxAbsoluteDegrees:F3}/{report.TangentAngleDelta.AverageAbsoluteDegrees:F3}, " +
                $"normal deg max/avg={report.NormalAngleDelta.MaxAbsoluteDegrees:F3}/{report.NormalAngleDelta.AverageAbsoluteDegrees:F3}, " +
                $"binormal deg max/avg={report.BinormalAngleDelta.MaxAbsoluteDegrees:F3}/{report.BinormalAngleDelta.AverageAbsoluteDegrees:F3}, " +
                $"twist deg max/avg={report.FrameTwistDelta.MaxAbsoluteDegrees:F3}/{report.FrameTwistDelta.AverageAbsoluteDegrees:F3}, " +
                $"|dCurvature| max/avg={report.CurvatureEstimateDelta.MaxAbsolute:F5}/{report.CurvatureEstimateDelta.AverageAbsolute:F5}, " +
                $"worst interval [{worstInterval.StartDistance:F3},{worstInterval.EndDistance:F3}] frameDeg={worstInterval.FrameAngleDeltaRadians * (180.0 / System.Math.PI):F3}.";

            Debug.Log(message, this);
        }

        private void LogSmoothnessComparisonDiagnostics(
            TrackFrameSmoothnessReport segmentedReport,
            TrackFrameSmoothnessReport smoothReport,
            int diagnosticSampleCount)
        {
            if (segmentedReport == null || segmentedReport.IntervalCount == 0 ||
                smoothReport == null || smoothReport.IntervalCount == 0)
            {
                Debug.Log(
                    "BackendTrainPipelineGizmoVisualizer smoothness comparison: smooth continuous diagnostics unavailable.",
                    this);
                return;
            }

            string message =
                "BackendTrainPipelineGizmoVisualizer smoothness comparison " +
                $"(diagnosticSamples={diagnosticSampleCount}, segmented->smooth): " +
                $"frame max deg {segmentedReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}->{smoothReport.FrameAngleDelta.MaxAbsoluteDegrees:F3}, " +
                $"tangent max deg {segmentedReport.TangentAngleDelta.MaxAbsoluteDegrees:F3}->{smoothReport.TangentAngleDelta.MaxAbsoluteDegrees:F3}, " +
                $"twist max deg {segmentedReport.FrameTwistDelta.MaxAbsoluteDegrees:F3}->{smoothReport.FrameTwistDelta.MaxAbsoluteDegrees:F3}, " +
                $"|dCurvature| max {segmentedReport.CurvatureEstimateDelta.MaxAbsolute:F5}->{smoothReport.CurvatureEstimateDelta.MaxAbsolute:F5}.";

            Debug.Log(message, this);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static Vector3 ToVector3(Vector3d vector)
        {
            return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }
    }
}


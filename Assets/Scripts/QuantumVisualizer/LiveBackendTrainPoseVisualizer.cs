using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;
using Quantum.Track;
using UnityEngine;
using TrackFrame = Quantum.Track.TrackFrame;

namespace QuantumVisualizer
{
    public sealed class LiveBackendTrainPoseVisualizer : MonoBehaviour
    {
        private const double DistanceMargin = 0.001;

        [Header("Train")]
        [SerializeField, Min(1)] private int carCount = 4;
        [SerializeField, Min(0.01f)] private float carSpacing = 5.5f;
        [SerializeField, Min(0.01f)] private float carLength = 4.5f;
        [SerializeField, Min(0.01f)] private float carWidth = 1.8f;
        [SerializeField, Min(0.01f)] private float carHeight = 1.4f;
        [SerializeField, Min(0.01f)] private float bogieSpacing = 2.8f;

        [Header("Playback")]
        [SerializeField, Min(0.0f)] private float speed = 6.0f;

        [Header("Gizmos")]
        [SerializeField, Min(2)] private int centerlineSampleCount = 80;
        [SerializeField, Min(1)] private int frameAxisStride = 8;
        [SerializeField, Min(0.01f)] private float frameAxisLength = 1.25f;

        private readonly List<GameObject> _carCubes = new List<GameObject>();

        private TrackEvaluator _evaluator;
        private TrainCarTransformProvider _provider;
        private TrainConsistDefinition _consist;
        private TrackFrame[] _centerlineFrames;
        private double _leadDistance;
        private double _minimumLeadDistance;
        private double _maximumLeadDistance;
        private string _lastWarning;
        private bool _ready;

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Initialize();
            }
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            AdvanceLeadDistance(speed * Time.deltaTime);
            EvaluateAndApplyPose();
        }

        private void OnDisable()
        {
            ResetRuntimeState();
        }

        private void OnDestroy()
        {
            ResetRuntimeState();
        }

        private void OnValidate()
        {
            carCount = Mathf.Max(1, carCount);
            carSpacing = Mathf.Max(0.01f, carSpacing);
            carLength = Mathf.Max(0.01f, carLength);
            carWidth = Mathf.Max(0.01f, carWidth);
            carHeight = Mathf.Max(0.01f, carHeight);
            bogieSpacing = Mathf.Clamp(bogieSpacing, 0.01f, carLength);
            speed = Mathf.Max(0.0f, speed);
            centerlineSampleCount = Mathf.Max(2, centerlineSampleCount);
            frameAxisStride = Mathf.Max(1, frameAxisStride);
            frameAxisLength = Mathf.Max(0.01f, frameAxisLength);
        }

        private void Initialize()
        {
            ResetRuntimeState();

            try
            {
                TrackDocument document = BuildTrackDocument();
                _evaluator = new TrackEvaluator(document);
                _provider = new TrainCarTransformProvider(_evaluator);
                _consist = BuildConsistDefinition();

                double totalLength = _evaluator.GetBoundTrackTotalLength();
                double bogieHalfSpacing = _consist.BogieSpacing * 0.5;
                _minimumLeadDistance =
                    ((_consist.CarCount - 1) * _consist.CarSpacing) + bogieHalfSpacing + DistanceMargin;
                _maximumLeadDistance = totalLength - bogieHalfSpacing - DistanceMargin;

                if (_minimumLeadDistance > _maximumLeadDistance)
                {
                    WarnOnce(
                        "Live backend train pose visualizer cannot place this consist: " +
                        $"required length is {_minimumLeadDistance + bogieHalfSpacing + DistanceMargin:F2}, " +
                        $"but the track length is {totalLength:F2}.");
                    return;
                }

                _centerlineFrames = SampleCenterlineFrames(totalLength);
                CreateCarCubes();
                _leadDistance = _minimumLeadDistance;
                _ready = true;
                EvaluateAndApplyPose();
            }
            catch (Exception ex)
            {
                WarnOnce("Live backend train pose visualizer failed to initialize: " + ex.Message);
                ResetRuntimeState(clearWarning: false);
            }
        }

        private static TrackDocument BuildTrackDocument()
        {
            var bezier = new CubicBezierCurve(
                new Vector3d(0.0, 0.0, 0.0),
                new Vector3d(14.0, 0.0, 0.0),
                new Vector3d(24.0, 9.0, 8.0),
                new Vector3d(40.0, 5.0, 20.0));
            var centerline = new ArcLengthCurveAdapter(bezier, samples: 256);

            return new TrackDocument(new TrackSegment[]
            {
                new CurvedSegment(
                    length: centerline.Length,
                    id: "live-visualizer-centerline",
                    spline: centerline,
                    rollRadians: System.Math.PI / 12.0)
            });
        }

        private TrainConsistDefinition BuildConsistDefinition()
        {
            var wheelLayout = new TrainWheelLayout(
                wheelCountPerBogie: 2,
                wheelRadius: carHeight * 0.2,
                wheelWidth: carWidth * 0.15,
                axleSpacing: bogieSpacing * 0.5);

            return new TrainConsistDefinition(
                carCount,
                carSpacing,
                carLength,
                carWidth,
                carHeight,
                bogieSpacing,
                wheelLayout);
        }

        private TrackFrame[] SampleCenterlineFrames(double totalLength)
        {
            var distances = new double[centerlineSampleCount];
            double denominator = centerlineSampleCount - 1;

            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = totalLength * (i / denominator);
            }

            return _evaluator.EvaluateFramesAtDistances(distances);
        }

        private void CreateCarCubes()
        {
            for (int i = 0; i < _consist.CarCount; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Live Backend Train Car {i}";
                cube.transform.SetParent(transform, false);
                cube.transform.localScale = new Vector3(carLength, carHeight, carWidth);
                _carCubes.Add(cube);
            }
        }

        private void AdvanceLeadDistance(double distanceDelta)
        {
            double travelRange = _maximumLeadDistance - _minimumLeadDistance;
            if (travelRange <= 0.0)
            {
                _leadDistance = _minimumLeadDistance;
                return;
            }

            _leadDistance += distanceDelta;
            while (_leadDistance > _maximumLeadDistance)
            {
                _leadDistance -= travelRange;
            }
        }

        private void EvaluateAndApplyPose()
        {
            try
            {
                TrainPoseResult pose = _provider.EvaluateTrainPose(_leadDistance, _consist);
                IReadOnlyList<ArticulatedTrainCarWithWheelsTransform> cars = pose.CarsReadOnly;

                if (cars.Count != _carCubes.Count)
                {
                    throw new InvalidOperationException(
                        $"Backend returned {cars.Count} cars for {_carCubes.Count} generated cubes.");
                }

                for (int i = 0; i < cars.Count; i++)
                {
                    TrackFrame frame = cars[i].Body.ArticulatedFrame;
                    Transform cubeTransform = _carCubes[i].transform;
                    cubeTransform.localPosition = ToUnityVector(frame.Position);
                    cubeTransform.localRotation = ToUnityRotation(frame);
                }

                _lastWarning = null;
            }
            catch (Exception ex)
            {
                WarnOnce("Live backend train pose evaluation failed: " + ex.Message);
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _centerlineFrames == null || _centerlineFrames.Length == 0)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 1; i < _centerlineFrames.Length; i++)
            {
                Gizmos.DrawLine(
                    transform.TransformPoint(ToUnityVector(_centerlineFrames[i - 1].Position)),
                    transform.TransformPoint(ToUnityVector(_centerlineFrames[i].Position)));
            }

            for (int i = 0; i < _centerlineFrames.Length; i += frameAxisStride)
            {
                DrawFrameAxes(_centerlineFrames[i]);
            }

            DrawFrameAxes(_centerlineFrames[_centerlineFrames.Length - 1]);
        }

        private void DrawFrameAxes(TrackFrame frame)
        {
            Vector3 origin = transform.TransformPoint(ToUnityVector(frame.Position));

            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, transform.TransformPoint(ToUnityVector(
                frame.Position + (frame.Tangent * frameAxisLength))));

            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, transform.TransformPoint(ToUnityVector(
                frame.Position + (frame.Normal * frameAxisLength))));

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, transform.TransformPoint(ToUnityVector(
                frame.Position + (frame.Binormal * frameAxisLength))));
        }

        private static Quaternion ToUnityRotation(TrackFrame frame)
        {
            return Quaternion.LookRotation(
                ToUnityVector(frame.Binormal),
                ToUnityVector(frame.Normal));
        }

        private static Vector3 ToUnityVector(Vector3d vector)
        {
            return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }

        private void WarnOnce(string message)
        {
            if (_lastWarning == message)
            {
                return;
            }

            _lastWarning = message;
            Debug.LogWarning(message, this);
        }

        private void ResetRuntimeState(bool clearWarning = true)
        {
            _ready = false;
            _evaluator = null;
            _provider = null;
            _consist = null;
            _centerlineFrames = null;

            for (int i = 0; i < _carCubes.Count; i++)
            {
                GameObject cube = _carCubes[i];
                if (cube == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(cube);
                }
                else
                {
                    DestroyImmediate(cube);
                }
            }

            _carCubes.Clear();
            if (clearWarning)
            {
                _lastWarning = null;
            }
        }
    }
}

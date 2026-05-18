using System.Collections.Generic;
using UnityEngine;

namespace QuantumVisualizer
{
    [ExecuteAlways]
    public sealed class TrainPoseGizmoVisualizer : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TextAsset poseJson;
        [SerializeField] private bool drawOnlyWhenSelected;
        [SerializeField] private bool logParseErrors = true;

        [Header("Feature Toggles")]
        [SerializeField] private bool drawTrackCenterline = true;
        [SerializeField] private bool drawBodyFrames = true;
        [SerializeField] private bool drawBogieFrames = true;
        [SerializeField] private bool drawWheelFrames = true;
        [SerializeField] private bool drawBodyToBogieLinks = true;
        [SerializeField] private bool drawBogieToWheelLinks = true;

        [Header("Style")]
        [SerializeField, Min(0.01f)] private float frameAxisLength = 0.5f;
        [SerializeField, Min(0.0f)] private float jointDotRadius = 0.04f;
        [SerializeField] private Color bodyLinkColor = new Color(1f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color bogieLinkColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color trackColor = new Color(0.2f, 0.95f, 1f, 1f);

        private string _cachedJsonText;
        private TrainPoseExportV1Dto _cachedPose;
        private string _lastError;

        private void OnValidate()
        {
            _cachedJsonText = null;
            _cachedPose = null;
            _lastError = null;
        }

        private void OnDrawGizmos()
        {
            if (!drawOnlyWhenSelected)
            {
                DrawPoseGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnlyWhenSelected)
            {
                DrawPoseGizmos();
            }
        }

        private void DrawPoseGizmos()
        {
            if (!TryGetPose(out TrainPoseExportV1Dto pose))
            {
                return;
            }

            if (pose.cars == null || pose.cars.Length == 0)
            {
                return;
            }

            if (drawTrackCenterline)
            {
                DrawTrackCenterline(pose);
            }

            for (int i = 0; i < pose.cars.Length; i++)
            {
                ArticulatedTrainCarWithWheelsV1Dto car = pose.cars[i];
                if (car == null)
                {
                    continue;
                }

                TrackFrameV1Dto bodyFrame = car.body != null ? car.body.originalBody != null ? car.body.originalBody.frame : null : null;
                TrackFrameV1Dto frontBogieFrame = ResolveBogieFrame(car, useFront: true);
                TrackFrameV1Dto rearBogieFrame = ResolveBogieFrame(car, useFront: false);

                if (drawBodyFrames)
                {
                    DrawFrameAxes(bodyFrame, frameAxisLength);
                }

                if (drawBogieFrames)
                {
                    DrawFrameAxes(frontBogieFrame, frameAxisLength * 0.9f);
                    DrawFrameAxes(rearBogieFrame, frameAxisLength * 0.9f);
                }

                if (drawBodyToBogieLinks)
                {
                    DrawLink(bodyFrame, frontBogieFrame, bodyLinkColor);
                    DrawLink(bodyFrame, rearBogieFrame, bodyLinkColor);
                }

                TrainBogieWithWheelsV1Dto frontBogie = car.frontBogie;
                TrainBogieWithWheelsV1Dto rearBogie = car.rearBogie;

                DrawWheelsForBogie(frontBogie, frontBogieFrame);
                DrawWheelsForBogie(rearBogie, rearBogieFrame);
            }
        }

        private void DrawWheelsForBogie(TrainBogieWithWheelsV1Dto bogieWithWheels, TrackFrameV1Dto bogieFrame)
        {
            if (bogieWithWheels == null || bogieWithWheels.wheels == null)
            {
                return;
            }

            for (int i = 0; i < bogieWithWheels.wheels.Length; i++)
            {
                WheelTransformV1Dto wheel = bogieWithWheels.wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                TrackFrameV1Dto wheelFrame = wheel.frame;

                if (drawWheelFrames)
                {
                    DrawFrameAxes(wheelFrame, frameAxisLength * 0.65f);
                }

                if (drawBogieToWheelLinks)
                {
                    DrawLink(bogieFrame, wheelFrame, bogieLinkColor);
                }
            }
        }

        private void DrawTrackCenterline(TrainPoseExportV1Dto pose)
        {
            List<TrackFrameV1Dto> points = CollectCenterlineFrames(pose);
            if (points.Count < 2)
            {
                return;
            }

            Gizmos.color = trackColor;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = ToVector3(points[i - 1].position);
                Vector3 b = ToVector3(points[i].position);
                Gizmos.DrawLine(a, b);
            }
        }

        private static List<TrackFrameV1Dto> CollectCenterlineFrames(TrainPoseExportV1Dto pose)
        {
            var points = new List<TrackFrameV1Dto>();

            AddFrames(points, pose.trackSamples);
            AddFrames(points, pose.sampledTrackFrames);
            AddFrames(points, pose.samples);

            if (points.Count >= 2)
            {
                points.Sort((a, b) => a.distance.CompareTo(b.distance));
                return points;
            }

            if (pose.cars != null)
            {
                for (int i = 0; i < pose.cars.Length; i++)
                {
                    ArticulatedTrainCarWithWheelsV1Dto car = pose.cars[i];
                    if (car == null)
                    {
                        continue;
                    }

                    if (car.body != null)
                    {
                        if (car.body.originalBody != null)
                        {
                            AddFrame(points, car.body.originalBody.frame);
                        }

                        AddFrame(points, car.body.frontBogie != null ? car.body.frontBogie.frame : null);
                        AddFrame(points, car.body.rearBogie != null ? car.body.rearBogie.frame : null);
                    }

                    AddFrame(points, car.frontBogie != null && car.frontBogie.bogie != null ? car.frontBogie.bogie.frame : null);
                    AddFrame(points, car.rearBogie != null && car.rearBogie.bogie != null ? car.rearBogie.bogie.frame : null);
                }
            }

            points.Sort((a, b) => a.distance.CompareTo(b.distance));
            return points;
        }

        private static void AddFrames(List<TrackFrameV1Dto> dst, TrackFrameV1Dto[] src)
        {
            if (src == null)
            {
                return;
            }

            for (int i = 0; i < src.Length; i++)
            {
                AddFrame(dst, src[i]);
            }
        }

        private static void AddFrame(List<TrackFrameV1Dto> dst, TrackFrameV1Dto frame)
        {
            if (frame == null || frame.position == null)
            {
                return;
            }

            dst.Add(frame);
        }

        private static TrackFrameV1Dto ResolveBogieFrame(ArticulatedTrainCarWithWheelsV1Dto car, bool useFront)
        {
            if (car == null)
            {
                return null;
            }

            TrainBogieWithWheelsV1Dto bogieWithWheels = useFront ? car.frontBogie : car.rearBogie;
            if (bogieWithWheels != null && bogieWithWheels.bogie != null && bogieWithWheels.bogie.frame != null)
            {
                return bogieWithWheels.bogie.frame;
            }

            if (car.body == null)
            {
                return null;
            }

            BogieTransformV1Dto bodyBogie = useFront ? car.body.frontBogie : car.body.rearBogie;
            return bodyBogie != null ? bodyBogie.frame : null;
        }

        private void DrawFrameAxes(TrackFrameV1Dto frame, float axisLength)
        {
            if (frame == null || frame.position == null)
            {
                return;
            }

            Vector3 p = ToVector3(frame.position);
            Vector3 t = NormalizeOrFallback(frame.tangent, Vector3.right);
            Vector3 n = NormalizeOrFallback(frame.normal, Vector3.up);
            Vector3 b = NormalizeOrFallback(frame.binormal, Vector3.forward);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(p, p + t * axisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(p, p + n * axisLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(p, p + b * axisLength);

            if (jointDotRadius > 0f)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(p, jointDotRadius);
            }
        }

        private static Vector3 NormalizeOrFallback(Vector3V1Dto source, Vector3 fallback)
        {
            if (source == null)
            {
                return fallback;
            }

            Vector3 v = ToVector3(source);
            return v.sqrMagnitude > 0.000001f ? v.normalized : fallback;
        }

        private void DrawLink(TrackFrameV1Dto a, TrackFrameV1Dto b, Color color)
        {
            if (a == null || b == null || a.position == null || b.position == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawLine(ToVector3(a.position), ToVector3(b.position));
        }

        private bool TryGetPose(out TrainPoseExportV1Dto pose)
        {
            pose = null;

            if (poseJson == null)
            {
                return false;
            }

            string text = poseJson.text;
            if (_cachedPose != null && string.Equals(text, _cachedJsonText, System.StringComparison.Ordinal))
            {
                pose = _cachedPose;
                return true;
            }

            if (!TrainPoseJsonLoader.TryLoad(poseJson, out TrainPoseExportV1Dto loaded, out string error))
            {
                if (logParseErrors && !string.Equals(_lastError, error, System.StringComparison.Ordinal))
                {
                    Debug.LogWarning("TrainPoseGizmoVisualizer: " + error, this);
                    _lastError = error;
                }

                _cachedPose = null;
                _cachedJsonText = text;
                return false;
            }

            _lastError = null;
            _cachedPose = loaded;
            _cachedJsonText = text;
            pose = loaded;
            return true;
        }

        private static Vector3 ToVector3(Vector3V1Dto v)
        {
            if (v == null)
            {
                return Vector3.zero;
            }

            return new Vector3(v.x, v.y, v.z);
        }
    }
}

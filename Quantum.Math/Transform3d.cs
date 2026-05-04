using System;

namespace Quantum.Math
{
    /// <summary>
    /// Minimal rigid transform with a rotation basis and translation.
    /// </summary>
    public readonly struct Transform3d
    {
        public Matrix3x3 Rotation { get; }

        public Vector3d Position { get; }

        public Transform3d(Matrix3x3 rotation, Vector3d position)
        {
            Rotation = rotation;
            Position = position;
        }

        public static Transform3d Identity => new Transform3d(Matrix3x3.Identity, Vector3d.Zero);

        public Vector3d TransformPoint(Vector3d local)
        {
            return Rotation.Multiply(local) + Position;
        }

        public Vector3d TransformDirection(Vector3d local)
        {
            return Rotation.Multiply(local);
        }

        public Transform3d Inverse()
        {
            Matrix3x3 inverseRotation = Rotation.Transpose();
            Vector3d inversePosition = inverseRotation.Multiply(Position * -1.0);
            return new Transform3d(inverseRotation, inversePosition);
        }

        public static Transform3d FromTrackFrame(ITrackFrameBasis frame, Vector3d position)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            Matrix3x3 rotation = Matrix3x3.FromBasis(frame.Tangent, frame.Normal, frame.Binormal);
            return new Transform3d(rotation, position);
        }
    }
}

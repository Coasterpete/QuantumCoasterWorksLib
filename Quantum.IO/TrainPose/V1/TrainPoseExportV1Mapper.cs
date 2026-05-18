using System;
using System.Numerics;
using Quantum.Math;
using Quantum.Track;

namespace Quantum.IO.TrainPose.V1
{
    /// <summary>
    /// Maps in-memory coaster train pose snapshots into TrainPoseExportV1 DTOs.
    /// </summary>
    public static class TrainPoseExportV1Mapper
    {
        /// <summary>
        /// Exports a complete train pose snapshot into the versioned JSON DTO boundary.
        /// </summary>
        public static TrainPoseExportV1Dto Export(TrainPoseResult source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Definition == null)
            {
                throw new ArgumentException("Source definition cannot be null.", nameof(source));
            }

            var carCount = source.CarsReadOnly.Count;
            var cars = new ArticulatedTrainCarWithWheelsV1Dto[carCount];

            for (int i = 0; i < carCount; i++)
            {
                cars[i] = MapArticulatedTrainCarWithWheels(source.CarsReadOnly[i]);
            }

            return new TrainPoseExportV1Dto
            {
                Contract = TrainPoseExportV1Dto.ContractName,
                Version = TrainPoseExportV1Dto.ContractVersion,
                LeadDistance = source.LeadDistance,
                Definition = MapDefinition(source.Definition),
                Cars = cars
            };
        }

        private static TrainConsistDefinitionV1Dto MapDefinition(TrainConsistDefinition source)
        {
            return new TrainConsistDefinitionV1Dto
            {
                CarCount = source.CarCount,
                CarSpacing = source.CarSpacing,
                CarGeometry = new TrainCarGeometryV1Dto
                {
                    Length = source.CarGeometry.Length,
                    Width = source.CarGeometry.Width,
                    Height = source.CarGeometry.Height
                },
                BogieLayout = new TrainBogieLayoutV1Dto
                {
                    BogieSpacing = source.BogieLayout.BogieSpacing
                },
                WheelLayout = MapWheelLayout(source.WheelLayout)
            };
        }

        private static TrainWheelLayoutV1Dto? MapWheelLayout(TrainWheelLayout? source)
        {
            if (source == null)
            {
                return null;
            }

            return new TrainWheelLayoutV1Dto
            {
                WheelCountPerBogie = source.WheelCountPerBogie,
                WheelRadius = source.WheelRadius,
                WheelWidth = source.WheelWidth,
                AxleSpacing = source.AxleSpacing
            };
        }

        private static ArticulatedTrainCarWithWheelsV1Dto MapArticulatedTrainCarWithWheels(ArticulatedTrainCarWithWheelsTransform source)
        {
            return new ArticulatedTrainCarWithWheelsV1Dto
            {
                Body = MapArticulatedTrainCar(source.Body),
                FrontBogie = MapTrainBogieWithWheels(source.FrontBogie),
                RearBogie = MapTrainBogieWithWheels(source.RearBogie)
            };
        }

        private static ArticulatedTrainCarV1Dto MapArticulatedTrainCar(ArticulatedTrainCarTransform source)
        {
            return new ArticulatedTrainCarV1Dto
            {
                OriginalBody = MapTrainCarTransform(source.OriginalBody),
                FrontBogie = MapBogieTransform(source.FrontBogie),
                RearBogie = MapBogieTransform(source.RearBogie),
                ArticulatedFrame = MapTrackFrame(source.ArticulatedFrame),
                ArticulatedMatrix = MapMatrix(source.ArticulatedMatrix),
                CenterDistance = source.CenterDistance
            };
        }

        private static TrainCarTransformV1Dto MapTrainCarTransform(TrainCarTransform source)
        {
            return new TrainCarTransformV1Dto
            {
                CarIndex = source.CarIndex,
                Distance = source.Distance,
                Frame = MapTrackFrame(source.Frame),
                Matrix = MapMatrix(source.Matrix)
            };
        }

        private static TrainBogieWithWheelsV1Dto MapTrainBogieWithWheels(TrainBogieWithWheelsTransform source)
        {
            var wheelCount = source.WheelsReadOnly.Count;
            var wheels = new WheelTransformV1Dto[wheelCount];

            for (int i = 0; i < wheelCount; i++)
            {
                wheels[i] = MapWheelTransform(source.WheelsReadOnly[i]);
            }

            return new TrainBogieWithWheelsV1Dto
            {
                Bogie = MapBogieTransform(source.Bogie),
                Wheels = wheels
            };
        }

        private static BogieTransformV1Dto MapBogieTransform(BogieTransform source)
        {
            return new BogieTransformV1Dto
            {
                CarIndex = source.CarIndex,
                BogieIndex = source.BogieIndex,
                Distance = source.Distance,
                Frame = MapTrackFrame(source.Frame),
                Matrix = MapMatrix(source.Matrix)
            };
        }

        private static WheelTransformV1Dto MapWheelTransform(WheelTransform source)
        {
            return new WheelTransformV1Dto
            {
                CarIndex = source.CarIndex,
                BogieIndex = source.BogieIndex,
                WheelIndex = source.WheelIndex,
                LocalOffsetX = source.LocalOffsetX,
                LocalOffsetY = source.LocalOffsetY,
                LocalOffsetZ = source.LocalOffsetZ,
                Frame = MapTrackFrame(source.Frame),
                Matrix = MapMatrix(source.Matrix)
            };
        }

        private static TrackFrameV1Dto MapTrackFrame(TrackFrame source)
        {
            return new TrackFrameV1Dto
            {
                Distance = source.Distance,
                Position = MapVector3d(source.Position),
                Tangent = MapVector3d(source.Tangent),
                Normal = MapVector3d(source.Normal),
                Binormal = MapVector3d(source.Binormal)
            };
        }

        private static Vector3dV1Dto MapVector3d(Vector3d source)
        {
            return new Vector3dV1Dto
            {
                X = source.X,
                Y = source.Y,
                Z = source.Z
            };
        }

        private static Matrix4x4V1Dto MapMatrix(Matrix4x4 source)
        {
            return new Matrix4x4V1Dto
            {
                M11 = source.M11,
                M12 = source.M12,
                M13 = source.M13,
                M14 = source.M14,
                M21 = source.M21,
                M22 = source.M22,
                M23 = source.M23,
                M24 = source.M24,
                M31 = source.M31,
                M32 = source.M32,
                M33 = source.M33,
                M34 = source.M34,
                M41 = source.M41,
                M42 = source.M42,
                M43 = source.M43,
                M44 = source.M44
            };
        }

        private static Matrix4x4V1Dto MapMatrix(Matrix4x4d source)
        {
            return new Matrix4x4V1Dto
            {
                M11 = source.M11,
                M12 = source.M12,
                M13 = source.M13,
                M14 = source.M14,
                M21 = source.M21,
                M22 = source.M22,
                M23 = source.M23,
                M24 = source.M24,
                M31 = source.M31,
                M32 = source.M32,
                M33 = source.M33,
                M34 = source.M34,
                M41 = source.M41,
                M42 = source.M42,
                M43 = source.M43,
                M44 = source.M44
            };
        }
    }
}

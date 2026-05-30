using System;
using System.Collections.Generic;
using System.Linq;
using Quantum.Math;

namespace Quantum.IO.GeometryInterchange
{
    public enum ExternalCurveKind
    {
        Unknown = 0,
        Polyline = 1,
        Bezier = 2,
        BSpline = 3,
        Nurbs = 4
    }

    public sealed class ExternalCurveDocumentMetadata
    {
        public ExternalCurveDocumentMetadata(
            string sourceName,
            string formatName,
            string? formatVersion = null,
            string units = "meters",
            string? adapterName = null)
        {
            SourceName = sourceName ?? string.Empty;
            FormatName = formatName ?? string.Empty;
            FormatVersion = formatVersion;
            Units = units ?? string.Empty;
            AdapterName = adapterName;
        }

        public string SourceName { get; }

        public string FormatName { get; }

        public string? FormatVersion { get; }

        public string Units { get; }

        public string? AdapterName { get; }
    }

    public sealed class ExternalCurveDocument
    {
        private readonly IReadOnlyList<ExternalCurveData> _curves;

        public ExternalCurveDocument(
            ExternalCurveDocumentMetadata metadata,
            IEnumerable<ExternalCurveData> curves)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (curves == null)
            {
                throw new ArgumentNullException(nameof(curves));
            }

            ExternalCurveData[] curveArray = curves.ToArray();

            for (int i = 0; i < curveArray.Length; i++)
            {
                if (curveArray[i] == null)
                {
                    throw new ArgumentException("Curve collection cannot contain null entries.", nameof(curves));
                }
            }

            Metadata = metadata;
            _curves = Array.AsReadOnly(curveArray);
        }

        public ExternalCurveDocumentMetadata Metadata { get; }

        public IReadOnlyList<ExternalCurveData> Curves => _curves;
    }

    public sealed class ExternalCurveData
    {
        private readonly IReadOnlyList<CurveControlPointData> _controlPoints;

        public ExternalCurveData(
            string id,
            ExternalCurveKind kind,
            CurveDegreeMetadata? degree,
            IEnumerable<CurveControlPointData> controlPoints,
            KnotVectorData? knotVector = null,
            string? name = null)
        {
            if (controlPoints == null)
            {
                throw new ArgumentNullException(nameof(controlPoints));
            }

            CurveControlPointData[] controlPointArray = controlPoints.ToArray();

            for (int i = 0; i < controlPointArray.Length; i++)
            {
                if (controlPointArray[i] == null)
                {
                    throw new ArgumentException("Control point collection cannot contain null entries.", nameof(controlPoints));
                }
            }

            Id = id ?? string.Empty;
            Name = name;
            Kind = kind;
            Degree = degree;
            KnotVector = knotVector;
            _controlPoints = Array.AsReadOnly(controlPointArray);
        }

        public string Id { get; }

        public string? Name { get; }

        public ExternalCurveKind Kind { get; }

        public CurveDegreeMetadata? Degree { get; }

        public IReadOnlyList<CurveControlPointData> ControlPoints => _controlPoints;

        public KnotVectorData? KnotVector { get; }
    }

    public sealed class CurveControlPointData
    {
        public CurveControlPointData(double x, double y, double z, double? weight = null)
        {
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));
            ValidateFinite(z, nameof(z));

            if (weight.HasValue)
            {
                ValidateFinite(weight.Value, nameof(weight));

                if (weight.Value <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be positive when supplied.");
                }
            }

            X = x;
            Y = y;
            Z = z;
            Weight = weight;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double? Weight { get; }

        public bool HasWeight => Weight.HasValue;

        public Vector3d Position => new Vector3d(X, Y, Z);

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
            }
        }
    }

    public sealed class CurveDegreeMetadata
    {
        public CurveDegreeMetadata(int degree)
            : this(degree, degree + 1)
        {
        }

        public CurveDegreeMetadata(int degree, int order)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, "Degree must be at least 1.");
            }

            if (order != degree + 1)
            {
                throw new ArgumentException("Curve order must equal degree + 1.", nameof(order));
            }

            Degree = degree;
            Order = order;
        }

        public int Degree { get; }

        public int Order { get; }
    }

    public sealed class KnotVectorData
    {
        private readonly IReadOnlyList<double> _knots;

        public KnotVectorData(IEnumerable<double> knots)
        {
            if (knots == null)
            {
                throw new ArgumentNullException(nameof(knots));
            }

            double[] knotArray = knots.ToArray();

            for (int i = 0; i < knotArray.Length; i++)
            {
                double knot = knotArray[i];

                if (double.IsNaN(knot) || double.IsInfinity(knot))
                {
                    throw new ArgumentOutOfRangeException(nameof(knots), knot, "Knot values must be finite.");
                }

                if (i > 0 && knot < knotArray[i - 1])
                {
                    throw new ArgumentException("Knot vector must be nondecreasing.", nameof(knots));
                }
            }

            _knots = Array.AsReadOnly(knotArray);
        }

        public IReadOnlyList<double> Knots => _knots;

        public int Count => _knots.Count;

        public double this[int index] => _knots[index];
    }
}

using System;
using System.Collections.Generic;
using Quantum.Math;
using Quantum.Splines;

namespace Quantum.FVD
{
    /// <summary>
    /// Minimal graph payload for NURBS-backed FVD centerline generation.
    /// </summary>
    public sealed class FvdGraph
    {
        private readonly List<FvdControlNode> _controlNodes;
        private readonly List<FvdForceSample> _forceSamples;
        private readonly List<FvdSectionDefinition> _sections;

        public IReadOnlyList<FvdControlNode> ControlNodes => _controlNodes;

        public IReadOnlyList<FvdForceSample> ForceSamples => _forceSamples;

        public IReadOnlyList<FvdSectionDefinition> Sections => _sections;

        public int Degree { get; }

        public FvdGraph(List<FvdControlNode> controlNodes, int degree)
            : this(controlNodes, degree, new List<FvdForceSample>(), new List<FvdSectionDefinition>())
        {
        }

        public FvdGraph(List<FvdControlNode> controlNodes, int degree, List<FvdForceSample> forceSamples)
            : this(controlNodes, degree, forceSamples, new List<FvdSectionDefinition>())
        {
        }

        public FvdGraph(
            List<FvdControlNode> controlNodes,
            int degree,
            List<FvdForceSample> forceSamples,
            List<FvdSectionDefinition> sections)
        {
            if (controlNodes == null)
                throw new ArgumentNullException(nameof(controlNodes));

            if (forceSamples == null)
                throw new ArgumentNullException(nameof(forceSamples));

            if (sections == null)
                throw new ArgumentNullException(nameof(sections));

            if (degree < 1)
                throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be at least 1.");

            if (controlNodes.Count < degree + 1)
            {
                throw new ArgumentException(
                    "Control node count must be at least degree + 1.",
                    nameof(controlNodes));
            }

            _controlNodes = new List<FvdControlNode>(controlNodes.Count);

            double previousU = double.NegativeInfinity;
            bool hasPrevious = false;

            for (int i = 0; i < controlNodes.Count; i++)
            {
                FvdControlNode node = controlNodes[i];

                ValidateU(node.U, i);
                ValidateWeight(node.Weight, i);

                if (hasPrevious && node.U <= previousU)
                {
                    throw new ArgumentException(
                        "Control node U values must be strictly increasing (sorted and non-duplicate).",
                        nameof(controlNodes));
                }

                _controlNodes.Add(node);
                previousU = node.U;
                hasPrevious = true;
            }

            _forceSamples = new List<FvdForceSample>(forceSamples.Count);

            previousU = double.NegativeInfinity;
            hasPrevious = false;

            for (int i = 0; i < forceSamples.Count; i++)
            {
                FvdForceSample sample = forceSamples[i];

                ValidateForceSampleU(sample.U, i);

                if (hasPrevious && sample.U <= previousU)
                {
                    throw new ArgumentException(
                        "Force sample U values must be strictly increasing (sorted and non-duplicate).",
                        nameof(forceSamples));
                }

                _forceSamples.Add(sample);
                previousU = sample.U;
                hasPrevious = true;
            }

            _sections = new List<FvdSectionDefinition>(sections.Count);

            for (int i = 0; i < sections.Count; i++)
            {
                FvdSectionDefinition section = sections[i] ?? throw new ArgumentException(
                    $"Section at index {i} cannot be null.",
                    nameof(sections));

                ValidateSectionOverlap(section, i);
                _sections.Add(section);
            }

            Degree = degree;
        }

        public FvdNurbsBuildResult BuildNurbsCurve(int arcLengthSamples)
        {
            if (arcLengthSamples < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arcLengthSamples),
                    "Arc-length sample count must be at least 2.");
            }

            var controlPoints = new List<Vector3d>(_controlNodes.Count);
            var weights = new List<double>(_controlNodes.Count);

            for (int i = 0; i < _controlNodes.Count; i++)
            {
                FvdControlNode node = _controlNodes[i];
                controlPoints.Add(node.Position);
                weights.Add(node.Weight);
            }

            var paramCurve = new NurbsCurve(controlPoints, weights, Degree);
            var arcCurve = new ArcLengthCurveAdapter(paramCurve, arcLengthSamples);

            return new FvdNurbsBuildResult(paramCurve, arcCurve);
        }

        private static void ValidateU(double u, int index)
        {
            if (double.IsNaN(u) || double.IsInfinity(u) || u < 0.0 || u > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(u),
                    $"Control node U at index {index} must be a finite value in [0, 1].");
            }
        }

        private static void ValidateWeight(double weight, int index)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight) || weight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(weight),
                    $"Control node weight at index {index} must be finite and > 0.");
            }
        }

        private static void ValidateForceSampleU(double u, int index)
        {
            if (double.IsNaN(u) || double.IsInfinity(u) || u < 0.0 || u > 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(u),
                    $"Force sample U at index {index} must be a finite value in [0, 1].");
            }
        }

        private void ValidateSectionOverlap(FvdSectionDefinition candidate, int candidateIndex)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                FvdSectionDefinition existing = _sections[i];

                if (existing.Kind != candidate.Kind || existing.Domain != candidate.Domain)
                    continue;

                // Treat section ranges as half-open intervals [StartX, EndX):
                // touching edges are allowed, true interior overlap is rejected.
                bool overlaps = candidate.StartX < existing.EndX
                                && existing.StartX < candidate.EndX;

                if (!overlaps)
                    continue;

                throw new ArgumentException(
                    $"Section at index {candidateIndex} overlaps section at index {i} for kind '{candidate.Kind}' and domain '{candidate.Domain}'.",
                    nameof(candidate));
            }
        }
    }
}

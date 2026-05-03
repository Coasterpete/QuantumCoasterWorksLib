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

        public double EvaluateSectionChannelAt(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            FvdSectionChannel channel,
            double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be a finite value.");
            }

            FvdSectionDefinition section = ResolveSectionForEvaluationOrThrow(kind, domain, x);
            return section.EvaluateAt(channel, x);
        }

        public bool TryEvaluateSectionChannelAt(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            FvdSectionChannel channel,
            double x,
            out double value)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be a finite value.");
            }

            if (!TryResolveSectionForEvaluation(kind, domain, x, out FvdSectionDefinition section))
            {
                value = default;
                return false;
            }

            IReadOnlyList<FvdSectionFunction> functions = section.Functions;
            for (int i = 0; i < functions.Count; i++)
            {
                FvdSectionFunction function = functions[i];
                if (function.Channel != channel)
                    continue;

                value = function.EvaluateAt(x);
                return true;
            }

            value = default;
            return false;
        }

        public IReadOnlyList<FvdChannelEvaluation> EvaluateSectionAllAt(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be a finite value.");
            }

            FvdSectionDefinition section = ResolveSectionForEvaluationOrThrow(kind, domain, x);
            return section.EvaluateAllAt(x);
        }

        public bool TryEvaluateSectionAllAt(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            double x,
            out IReadOnlyList<FvdChannelEvaluation> evaluations)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "Evaluation X must be a finite value.");
            }

            if (!TryResolveSectionForEvaluation(kind, domain, x, out FvdSectionDefinition section))
            {
                evaluations = Array.Empty<FvdChannelEvaluation>();
                return false;
            }

            evaluations = section.EvaluateAllAt(x);
            return true;
        }

        public bool TryEvaluateForceTargetsAt(
            FvdFunctionDomain domain,
            double x,
            out double normalG,
            out double lateralG,
            out double rollRateDegPerSec)
        {
            if (!TryEvaluateSectionAllAt(FvdSectionKind.Force, domain, x, out IReadOnlyList<FvdChannelEvaluation> evaluations))
            {
                normalG = default;
                lateralG = default;
                rollRateDegPerSec = default;
                return false;
            }

            bool hasNormal = false;
            bool hasLateral = false;
            bool hasRollRate = false;
            double normalValue = default;
            double lateralValue = default;
            double rollRateValue = default;

            for (int i = 0; i < evaluations.Count; i++)
            {
                FvdChannelEvaluation evaluation = evaluations[i];

                switch (evaluation.Channel)
                {
                    case FvdSectionChannel.NormalG:
                        hasNormal = true;
                        normalValue = evaluation.Value;
                        break;

                    case FvdSectionChannel.LateralG:
                        hasLateral = true;
                        lateralValue = evaluation.Value;
                        break;

                    case FvdSectionChannel.RollRateDegPerSec:
                        hasRollRate = true;
                        rollRateValue = evaluation.Value;
                        break;
                }
            }

            if (!(hasNormal && hasLateral && hasRollRate))
            {
                normalG = default;
                lateralG = default;
                rollRateDegPerSec = default;
                return false;
            }

            normalG = normalValue;
            lateralG = lateralValue;
            rollRateDegPerSec = rollRateValue;
            return true;
        }

        private bool TryResolveSectionForEvaluation(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            double x,
            out FvdSectionDefinition section)
        {
            var matchingSections = new List<FvdSectionDefinition>();
            FvdSectionDefinition? finalSection = null;

            for (int i = 0; i < _sections.Count; i++)
            {
                FvdSectionDefinition candidate = _sections[i];
                if (candidate.Kind != kind || candidate.Domain != domain)
                    continue;

                matchingSections.Add(candidate);

                if (finalSection == null || candidate.EndX > finalSection.EndX)
                    finalSection = candidate;
            }

            for (int i = 0; i < matchingSections.Count; i++)
            {
                FvdSectionDefinition candidate = matchingSections[i];
                if (x >= candidate.StartX && x < candidate.EndX)
                {
                    section = candidate;
                    return true;
                }
            }

            if (finalSection != null && x == finalSection.EndX && x >= finalSection.StartX)
            {
                section = finalSection;
                return true;
            }

            section = null!;
            return false;
        }

        private FvdSectionDefinition ResolveSectionForEvaluationOrThrow(
            FvdSectionKind kind,
            FvdFunctionDomain domain,
            double x)
        {
            if (TryResolveSectionForEvaluation(kind, domain, x, out FvdSectionDefinition section))
                return section;

            throw new InvalidOperationException(
                $"No section exists for kind '{kind}', domain '{domain}', and x={x}.");
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

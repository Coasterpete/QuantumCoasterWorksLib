using System;
using Quantum.Physics;

namespace Quantum.FVD
{
    /// <summary>
    /// Read-only adapter that exposes FVD force targets to physics.
    /// </summary>
    public sealed class FvdForceTargetProviderAdapter : IForceTargetProvider
    {
        private readonly FvdGraph _graph;
        private readonly FvdFunctionDomain _domain;

        public FvdForceTargetProviderAdapter(FvdGraph graph, FvdFunctionDomain domain)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _domain = domain;
        }

        public bool TryGetForceTargets(double x, out ForceTargets targets)
        {
            if (!_graph.TryEvaluateForceTargetsPermissiveAt(
                _domain,
                x,
                out double normalG,
                out double lateralG,
                out double rollRateDegPerSec))
            {
                targets = default;
                return false;
            }

            // Preserve adapter semantics: successful reads require a NormalG target.
            if (!_graph.TryEvaluateSectionChannelAt(
                FvdSectionKind.Force,
                _domain,
                FvdSectionChannel.NormalG,
                x,
                out _))
            {
                targets = default;
                return false;
            }

            targets = new ForceTargets(normalG, lateralG, rollRateDegPerSec);
            return true;
        }
    }
}

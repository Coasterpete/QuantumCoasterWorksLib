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
            if (!_graph.TryEvaluateSectionChannelAt(
                FvdSectionKind.Force,
                _domain,
                FvdSectionChannel.NormalG,
                x,
                out double normalG))
            {
                targets = default;
                return false;
            }

            double lateralG = 0.0;
            if (_graph.TryEvaluateSectionChannelAt(
                FvdSectionKind.Force,
                _domain,
                FvdSectionChannel.LateralG,
                x,
                out double lateralFromFvd))
            {
                lateralG = lateralFromFvd;
            }

            targets = new ForceTargets(normalG, lateralG, rollRateDegPerSec: 0.0);
            return true;
        }
    }
}

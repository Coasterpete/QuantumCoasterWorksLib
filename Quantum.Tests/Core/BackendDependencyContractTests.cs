using System;
using System.Reflection;

namespace Quantum.Tests;

public sealed class BackendDependencyContractTests
{
    private static readonly string[] BackendAssemblyNames =
    {
        "Quantum.Core",
        "Quantum.Math",
        "Quantum.Splines",
        "Quantum.Track",
        "Quantum.Physics",
        "Quantum.FVD",
        "Quantum.IO",
        "Quantum.Debug"
    };

    private static readonly string[] ForbiddenFrontendOrRendererPrefixes =
    {
        "UnityEngine",
        "UnityEditor",
        "Unreal",
        "Avalonia",
        "Silk.NET",
        "OpenTK",
        "Veldrid"
    };

    [Fact]
    public void QuantumBackendAssemblies_DoNotReferenceFrontendOrRendererAssemblies()
    {
        foreach (string assemblyName in BackendAssemblyNames)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));

            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                foreach (string forbiddenPrefix in ForbiddenFrontendOrRendererPrefixes)
                {
                    Assert.False(
                        reference.Name != null &&
                        reference.Name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase),
                        $"{assembly.GetName().Name} must not reference frontend or renderer assembly '{reference.Name}'.");
                }
            }
        }
    }
}

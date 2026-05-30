using System;
using System.IO;
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

    private static readonly string[] BackendProjectPaths =
    {
        "Quantum.Core/Quantum.Core.csproj",
        "Quantum.Math/Quantum.Math.csproj",
        "Quantum.Splines/Quantum.Splines.csproj",
        "Quantum.Track/Quantum.Track.csproj",
        "Quantum.Physics/Quantum.Physics.csproj",
        "Quantum.FVD/Quantum.FVD.csproj",
        "Quantum.IO/Quantum.IO.csproj",
        "Quantum.Debug/Quantum.Debug.csproj"
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

    private static readonly string[] ForbiddenGeometryInterchangePrefixes =
    {
        "Rhino",
        "Rhino3dm",
        "openNURBS",
        "OpenNurbs"
    };

    private static readonly string[] ForbiddenFrontendOrBrowserPackageFragments =
    {
        "Unity",
        "Unreal",
        "Avalonia",
        "Silk.NET",
        "OpenTK",
        "Veldrid",
        "Blazor",
        "Playwright",
        "Selenium",
        "Puppeteer",
        "Electron",
        "Microsoft.AspNetCore"
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

    [Fact]
    public void QuantumBackendAssemblies_DoNotReferenceRhinoOrOpenNurbsAssemblies()
    {
        foreach (string assemblyName in BackendAssemblyNames)
        {
            Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));

            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                foreach (string forbiddenPrefix in ForbiddenGeometryInterchangePrefixes)
                {
                    Assert.False(
                        reference.Name != null &&
                        reference.Name.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase),
                        $"{assembly.GetName().Name} must not directly reference Rhino/openNURBS assembly '{reference.Name}'.");
                }
            }
        }
    }

    [Fact]
    public void QuantumBackendProjects_DoNotReferenceFrontendOrBrowserPackages()
    {
        string repoRoot = ResolveRepositoryRoot();

        foreach (string projectPath in BackendProjectPaths)
        {
            string resolvedProjectPath = Path.Combine(repoRoot, ToPlatformPath(projectPath));
            string projectXml = File.ReadAllText(resolvedProjectPath);

            foreach (string forbiddenFragment in ForbiddenFrontendOrBrowserPackageFragments)
            {
                Assert.DoesNotContain(
                    forbiddenFragment,
                    projectXml,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string solutionPath = Path.Combine(directory.FullName, "QuantumCoasterWorks.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not resolve repository root from test output directory.");
    }

    private static string ToPlatformPath(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar);
    }
}

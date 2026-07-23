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
        "Quantum.Debug",
        "Quantum.Application"
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
        "Quantum.Debug/Quantum.Debug.csproj",
        "Quantum.Application/Quantum.Application.csproj"
    };

    private static readonly string[] ForbiddenFrontendOrRendererNames =
    {
        "Unity",
        "UnityEngine",
        "UnityEditor",
        "Unreal",
        "Avalonia",
        "Silk.NET",
        "OpenTK",
        "Veldrid",
        "Renderer",
        "Frontend"
    };

    private static readonly string[] ForbiddenGeometryInterchangePrefixes =
    {
        "Rhino",
        "Rhino3dm",
        "openNURBS",
        "OpenNurbs"
    };

    private static readonly string[] ForbiddenFrontendOrBrowserDependencyNames =
    {
        "Unity",
        "UnityEngine",
        "UnityEditor",
        "Unreal",
        "Avalonia",
        "Silk.NET",
        "OpenTK",
        "Veldrid",
        "Renderer",
        "Frontend",
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
                foreach (string forbiddenName in ForbiddenFrontendOrRendererNames)
                {
                    Assert.False(
                        reference.Name != null &&
                        ContainsDependencyName(reference.Name, forbiddenName),
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
    public void QuantumApplication_ReferencesOnlyTrackAndPersistenceQuantumAssemblies()
    {
        Assembly application = Assembly.Load(new AssemblyName("Quantum.Application"));
        string[] quantumReferences = application.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null && name.StartsWith("Quantum.", StringComparison.Ordinal))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Quantum.IO", "Quantum.Track" }, quantumReferences);
    }

    [Fact]
    public void QuantumBackendProjects_DoNotReferenceFrontendOrBrowserPackages()
    {
        string repoRoot = ResolveRepositoryRoot();

        foreach (string projectPath in BackendProjectPaths)
        {
            string resolvedProjectPath = Path.Combine(repoRoot, ToPlatformPath(projectPath));
            string projectXml = File.ReadAllText(resolvedProjectPath);

            foreach (string forbiddenName in ForbiddenFrontendOrBrowserDependencyNames)
            {
                Assert.False(
                    ContainsDependencyName(projectXml, forbiddenName),
                    $"{projectPath} must not reference frontend, renderer, browser, or engine dependency '{forbiddenName}'.");
            }
        }
    }

    private static bool ContainsDependencyName(string dependencyName, string forbiddenName)
    {
        return dependencyName.Equals(forbiddenName, StringComparison.OrdinalIgnoreCase) ||
            dependencyName.StartsWith(forbiddenName + ".", StringComparison.OrdinalIgnoreCase) ||
            dependencyName.StartsWith(forbiddenName + "-", StringComparison.OrdinalIgnoreCase) ||
            dependencyName.Contains("." + forbiddenName, StringComparison.OrdinalIgnoreCase) ||
            dependencyName.Contains("-" + forbiddenName, StringComparison.OrdinalIgnoreCase) ||
            dependencyName.Contains(forbiddenName + ".", StringComparison.OrdinalIgnoreCase) ||
            dependencyName.Contains(forbiddenName + "-", StringComparison.OrdinalIgnoreCase) ||
            dependencyName.Contains("\"" + forbiddenName + "\"", StringComparison.OrdinalIgnoreCase);
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

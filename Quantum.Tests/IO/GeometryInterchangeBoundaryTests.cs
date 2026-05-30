using System;
using Quantum.IO.GeometryInterchange;

namespace Quantum.Tests;

public sealed class GeometryInterchangeBoundaryTests
{
    [Fact]
    public void NurbsStyleCurveData_RepresentsDegreeOrderWeightsAndKnots()
    {
        var degree = new CurveDegreeMetadata(degree: 3);
        var knotVector = new KnotVectorData(new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0 });
        var curve = new ExternalCurveData(
            id: "centerline-a",
            kind: ExternalCurveKind.Nurbs,
            degree: degree,
            controlPoints: new[]
            {
                new CurveControlPointData(0.0, 0.0, 0.0, weight: 1.0),
                new CurveControlPointData(5.0, 2.0, 0.0, weight: 0.75),
                new CurveControlPointData(10.0, -1.0, 1.0, weight: 1.25),
                new CurveControlPointData(15.0, 0.0, 2.0, weight: 1.0)
            },
            knotVector: knotVector,
            name: "self-authored-centerline");

        var document = new ExternalCurveDocument(
            new ExternalCurveDocumentMetadata(
                sourceName: "synthetic-nurbs-fixture",
                formatName: "test-fixture",
                formatVersion: "1",
                units: "meters"),
            new[] { curve });

        ExternalCurveData actualCurve = Assert.Single(document.Curves);
        Assert.Equal("synthetic-nurbs-fixture", document.Metadata.SourceName);
        Assert.Equal("meters", document.Metadata.Units);
        Assert.Equal(ExternalCurveKind.Nurbs, actualCurve.Kind);
        Assert.Equal("self-authored-centerline", actualCurve.Name);
        Assert.Equal(3, actualCurve.Degree!.Degree);
        Assert.Equal(4, actualCurve.Degree.Order);
        Assert.Equal(4, actualCurve.ControlPoints.Count);
        Assert.True(actualCurve.ControlPoints[1].HasWeight);
        Assert.Equal(0.75, actualCurve.ControlPoints[1].Weight);
        Assert.Equal(5.0, actualCurve.ControlPoints[1].Position.X);
        Assert.Equal(2.0, actualCurve.ControlPoints[1].Position.Y);
        Assert.Equal(0.0, actualCurve.ControlPoints[1].Position.Z);
        Assert.NotNull(actualCurve.KnotVector);
        Assert.Equal(8, actualCurve.KnotVector!.Count);
        Assert.Equal(0.0, actualCurve.KnotVector[0]);
        Assert.Equal(1.0, actualCurve.KnotVector[7]);
    }

    [Fact]
    public void BSplineStyleCurveData_CanOmitWeightsButKeepDegreeAndKnots()
    {
        var curve = new ExternalCurveData(
            id: "bspline-a",
            kind: ExternalCurveKind.BSpline,
            degree: new CurveDegreeMetadata(degree: 2),
            controlPoints: new[]
            {
                new CurveControlPointData(0.0, 0.0, 0.0),
                new CurveControlPointData(2.0, 3.0, 0.0),
                new CurveControlPointData(4.0, 3.0, 0.0)
            },
            knotVector: new KnotVectorData(new[] { 0.0, 0.0, 0.0, 1.0, 1.0, 1.0 }));

        Assert.Equal(ExternalCurveKind.BSpline, curve.Kind);
        Assert.Equal(2, curve.Degree!.Degree);
        Assert.Equal(3, curve.Degree.Order);
        Assert.All(curve.ControlPoints, point => Assert.False(point.HasWeight));
        Assert.Equal(6, curve.KnotVector!.Count);
    }

    [Fact]
    public void Rhino3dmPlaceholderImport_ReturnsDeterministicUnsupportedDiagnostic()
    {
        var adapter = new Rhino3dmGeometryAdapter();
        var metadata = new ExternalCurveDocumentMetadata(
            sourceName: "placeholder.3dm",
            formatName: Rhino3dmGeometryAdapter.StableFormatName,
            adapterName: adapter.AdapterName);

        GeometryImportResult first = adapter.Import(Array.Empty<byte>(), metadata);
        GeometryImportResult second = adapter.Import(Array.Empty<byte>(), metadata);

        Assert.False(first.Success);
        Assert.Equal(GeometryInterchangeResultStatus.Unsupported, first.Status);
        Assert.Null(first.Document);
        GeometryInterchangeDiagnostic diagnostic = Assert.Single(first.Diagnostics);
        GeometryInterchangeDiagnostic repeatedDiagnostic = Assert.Single(second.Diagnostics);

        Assert.Equal(GeometryInterchangeDiagnosticCode.UnsupportedAdapter, diagnostic.Code);
        Assert.Equal(GeometryInterchangeDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(Rhino3dmGeometryAdapter.StableAdapterName, diagnostic.AdapterName);
        Assert.Equal("import", diagnostic.Operation);
        Assert.Equal(diagnostic.Message, repeatedDiagnostic.Message);
        Assert.Contains("placeholder", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rhino3dm/openNURBS", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rhino3dmPlaceholderExport_ReturnsDeterministicUnsupportedDiagnostic()
    {
        var adapter = new Rhino3dmGeometryAdapter();
        var document = new ExternalCurveDocument(
            new ExternalCurveDocumentMetadata(
                sourceName: "self-authored-empty-document",
                formatName: "quantum.geometry-interchange",
                units: "meters"),
            Array.Empty<ExternalCurveData>());

        GeometryExportResult first = adapter.Export(document);
        GeometryExportResult second = adapter.Export(document);

        Assert.False(first.Success);
        Assert.Empty(first.Payload);
        Assert.Equal(GeometryInterchangeResultStatus.Unsupported, first.Status);
        GeometryInterchangeDiagnostic diagnostic = Assert.Single(first.Diagnostics);
        GeometryInterchangeDiagnostic repeatedDiagnostic = Assert.Single(second.Diagnostics);

        Assert.Equal(GeometryInterchangeDiagnosticCode.UnsupportedAdapter, diagnostic.Code);
        Assert.Equal(GeometryInterchangeDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(Rhino3dmGeometryAdapter.StableAdapterName, diagnostic.AdapterName);
        Assert.Equal("export", diagnostic.Operation);
        Assert.Equal(diagnostic.Message, repeatedDiagnostic.Message);
        Assert.Contains("placeholder", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rhino3dm/openNURBS", diagnostic.Message, StringComparison.Ordinal);
    }
}

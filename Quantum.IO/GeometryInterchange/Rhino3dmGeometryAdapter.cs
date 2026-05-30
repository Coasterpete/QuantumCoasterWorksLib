using System;

namespace Quantum.IO.GeometryInterchange
{
    /// <summary>
    /// Placeholder boundary for future rhino3dm/openNURBS interchange.
    /// </summary>
    public sealed class Rhino3dmGeometryAdapter : IGeometryInterchangeAdapter
    {
        public const string StableAdapterName = "Rhino3dmGeometryAdapter";

        public const string StableFormatName = "rhino3dm/openNURBS";

        public string AdapterName => StableAdapterName;

        public string FormatName => StableFormatName;

        public GeometryImportResult Import(byte[] payload, ExternalCurveDocumentMetadata? metadata = null)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return GeometryImportResult.Unsupported(CreateUnsupportedDiagnostic("import"));
        }

        public GeometryExportResult Export(ExternalCurveDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return GeometryExportResult.Unsupported(CreateUnsupportedDiagnostic("export"));
        }

        private static GeometryInterchangeDiagnostic CreateUnsupportedDiagnostic(string operation)
        {
            return new GeometryInterchangeDiagnostic(
                GeometryInterchangeDiagnosticCode.UnsupportedAdapter,
                GeometryInterchangeDiagnosticSeverity.Error,
                StableAdapterName,
                operation,
                StableAdapterName + " is a placeholder. Add a deliberate rhino3dm/openNURBS backend dependency before enabling " + operation + ".");
        }
    }
}

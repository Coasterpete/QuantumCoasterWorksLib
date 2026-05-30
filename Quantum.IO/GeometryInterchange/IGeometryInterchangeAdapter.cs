namespace Quantum.IO.GeometryInterchange
{
    public interface IGeometryInterchangeAdapter
    {
        string AdapterName { get; }

        string FormatName { get; }

        GeometryImportResult Import(byte[] payload, ExternalCurveDocumentMetadata? metadata = null);

        GeometryExportResult Export(ExternalCurveDocument document);
    }
}

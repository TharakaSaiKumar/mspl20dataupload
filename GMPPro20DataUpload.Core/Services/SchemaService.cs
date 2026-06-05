using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class SchemaService : ISchemaService
{
    private readonly IExcelService _excelService;

    public SchemaService(IExcelService excelService)
    {
        _excelService = excelService;
    }

    public List<SchemaRow> LoadSchema(string schemaFilePath)
        => throw new NotImplementedException();

    public List<SchemaRow> GetRowsForCollection(List<SchemaRow> schema, string collectionName)
        => throw new NotImplementedException();

    public List<string> GetCollectionOrder(List<SchemaRow> schema)
        => throw new NotImplementedException();
}

using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.Core.Services;

public class ExcelService : IExcelService
{
    public List<Dictionary<string, string>> ReadDataRows(string filePath)
        => throw new NotImplementedException();

    public List<SchemaRow> ReadSchemaRows(string filePath)
        => throw new NotImplementedException();

    public void WriteOutputFile(string sourcePath, string destinationPath, List<ProcessResult> results)
        => throw new NotImplementedException();
}

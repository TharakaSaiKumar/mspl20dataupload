using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;

namespace GMPPro20DataUpload.UI;

public partial class Form1 : Form
{
    private readonly IProcessingService _processingService;
    private readonly IValidationService _validationService;
    private readonly MongoConfiguration _mongoConfig;
    private readonly ApplicationSettings _appSettings;

    public Form1(
        IProcessingService processingService,
        IValidationService validationService,
        MongoConfiguration mongoConfig,
        ApplicationSettings appSettings)
    {
        _processingService = processingService;
        _validationService = validationService;
        _mongoConfig       = mongoConfig;
        _appSettings       = appSettings;
        InitializeComponent();
    }

    // TODO: TEMP — developer test harness; remove after end-to-end testing is complete
    private async void BtnRunCoreTest_Click(object sender, EventArgs e)
    {
        // Hardcoded paths for local developer testing
        const string schemaPath       = @"C:\TestData\Schema.xlsx";
        const string dataPath         = @"C:\TestData\Data.xlsx";
        const string templateDirectory = @"Templates";

        _btnRunCoreTest.Enabled = false;

        try
        {
            // Step 1 — Validation
            ValidationResult validation = await _validationService.ValidateAsync(
                schemaPath,
                dataPath,
                templateDirectory,
                _mongoConfig);

            if (!validation.IsValid)
            {
                string errors = string.Join(Environment.NewLine, validation.Errors);
                MessageBox.Show(
                    "Validation failed:\n\n" + errors,
                    "Validation Errors",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Step 2 — Processing
            var progress = new Progress<string>(msg => System.Diagnostics.Debug.WriteLine(msg));

            ProcessingContext ctx = await _processingService.ProcessAsync(
                schemaPath,
                dataPath,
                templateDirectory,
                _mongoConfig,
                progress,
                CancellationToken.None);

            int successCount = ctx.Results.Count(r => r.IsSuccess);
            int failedCount  = ctx.Results.Count(r => !r.IsSuccess);

            MessageBox.Show(
                $"Processing complete.\n\n" +
                $"Total Rows    : {ctx.TotalRows}\n" +
                $"Processed Rows: {ctx.ProcessedRows}\n" +
                $"Success       : {successCount}\n" +
                $"Failed        : {failedCount}\n" +
                $"Aborted       : {ctx.IsAborted}",
                "Test Result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unhandled exception:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                "Test Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _btnRunCoreTest.Enabled = true;
        }
    }
}

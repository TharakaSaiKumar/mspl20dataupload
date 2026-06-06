using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text;

namespace GMPPro20DataUpload.UI;

public partial class Form1 : Form
{
    private readonly IProcessingService _processingService;
    private readonly IValidationService _validationService;
    private readonly IMongoService _mongoService;
    private readonly MongoConfiguration _mongoConfig;
    private readonly ApplicationSettings _appSettings;

    private CancellationTokenSource? _cts;
    private bool _validationPassed = false;

    public Form1(
        IProcessingService processingService,
        IValidationService validationService,
        IMongoService mongoService,
        MongoConfiguration mongoConfig,
        ApplicationSettings appSettings)
    {
        _processingService = processingService;
        _validationService = validationService;
        _mongoService      = mongoService;
        _mongoConfig       = mongoConfig;
        _appSettings       = appSettings;
        InitializeComponent();
    }

    // -------------------------------------------------------------------------
    // File pickers
    // -------------------------------------------------------------------------

    private void BtnBrowseSchema_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Schema Excel File",
            Filter = "Excel Files (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtSchemaPath.Text = dlg.FileName;
            ResetValidationState();
        }
    }

    private void BtnBrowseData_Click(object sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Data Excel File",
            Filter = "Excel Files (*.xlsx)|*.xlsx"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtDataPath.Text = dlg.FileName;
            ResetValidationState();
        }
    }

    private void BtnBrowseOutput_Click(object sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title    = "Save Output Excel File",
            Filter   = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "Output.xlsx"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtOutputPath.Text = dlg.FileName;
    }

    // -------------------------------------------------------------------------
    // Test Connection
    // -------------------------------------------------------------------------

    private async void BtnTestConnection_Click(object sender, EventArgs e)
    {
        _btnTestConnection.Enabled = false;
        try
        {
            bool ok = await _mongoService.TestConnectionAsync(_mongoConfig);
            AppendStatus(ok ? "MongoDB connection successful." : "MongoDB connection failed.");
        }
        catch (Exception ex)
        {
            AppendStatus($"Connection error: {ex.Message}");
        }
        finally
        {
            _btnTestConnection.Enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Validate
    // -------------------------------------------------------------------------

    private async void BtnValidate_Click(object sender, EventArgs e)
    {
        string schemaPath = _txtSchemaPath.Text.Trim();
        string dataPath   = _txtDataPath.Text.Trim();

        if (string.IsNullOrEmpty(schemaPath) || string.IsNullOrEmpty(dataPath))
        {
            AppendStatus("Please select both Schema and Data files before validating.");
            return;
        }

        _btnValidate.Enabled = false;
        _validationPassed    = false;
        _btnProcess.Enabled  = false;

        try
        {
            AppendStatus("Validating...");

            ValidationResult result = await _validationService.ValidateAsync(
                schemaPath,
                dataPath,
                _appSettings.TemplateDirectory,
                _mongoConfig);

            if (!result.IsValid)
            {
                AppendStatus("Validation failed:");
                foreach (string error in result.Errors)
                    AppendStatus($"  {error}");
            }
            else
            {
                AppendStatus("Validation passed.");
                _validationPassed   = true;
                _btnProcess.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            AppendStatus($"Validation error: {ex.Message}");
        }
        finally
        {
            _btnValidate.Enabled = true;
        }
    }

    // -------------------------------------------------------------------------
    // Process
    // -------------------------------------------------------------------------

    private async void BtnProcess_Click(object sender, EventArgs e)
    {
        if (!_validationPassed)
        {
            AppendStatus("Run validation first.");
            return;
        }

        string schemaPath = _txtSchemaPath.Text.Trim();
        string dataPath   = _txtDataPath.Text.Trim();
        string outputPath = _txtOutputPath.Text.Trim();

        if (string.IsNullOrEmpty(schemaPath) || string.IsNullOrEmpty(dataPath))
        {
            AppendStatus("Schema and Data file paths are required.");
            return;
        }
        if (string.IsNullOrEmpty(outputPath))
        {
            AppendStatus("Please select an output file path before processing.");
            return;
        }

        SetProcessingState(running: true);
        _txtSummary.Clear();
        _progressBar.Style = ProgressBarStyle.Marquee;
        _cts = new CancellationTokenSource();

        try
        {
            AppendStatus("Processing started.");

            var progress = new Progress<string>(msg =>
            {
                AppendStatus(msg);

                // Parse "Processing row N of M" to drive the progress bar
                if (msg.StartsWith("Processing row ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = msg.Split(' ');
                    if (parts.Length >= 5
                        && int.TryParse(parts[2], out int n)
                        && int.TryParse(parts[4], out int m)
                        && m > 0)
                    {
                        int pct = n * 100 / m;
                        if (_progressBar.Style != ProgressBarStyle.Continuous)
                            _progressBar.Style = ProgressBarStyle.Continuous;
                        _progressBar.Value = Math.Min(pct, 100);
                    }
                }
            });

            ProcessingContext ctx = await _processingService.ProcessAsync(
                schemaPath,
                dataPath,
                _appSettings.TemplateDirectory,
                outputPath,
                _mongoConfig,
                progress,
                _cts.Token);

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;

            int successCount = ctx.Results.Count(r => r.IsSuccess);
            int failedCount  = ctx.Results.Count(r => !r.IsSuccess);

            var sb = new StringBuilder();
            sb.AppendLine("Processing complete.");
            sb.AppendLine();
            sb.AppendLine($"Total Rows    : {ctx.TotalRows}");
            sb.AppendLine($"Processed Rows: {ctx.ProcessedRows}");
            sb.AppendLine($"Success       : {successCount}");
            sb.AppendLine($"Failed        : {failedCount}");
            sb.Append(    $"Aborted       : {ctx.IsAborted}");

            List<ProcessResult> failedRows = ctx.Results.Where(r => !r.IsSuccess).ToList();
            if (failedRows.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("Failed Rows:");
                foreach (ProcessResult fr in failedRows)
                {
                    sb.AppendLine();
                    sb.Append($"  Row {fr.RowNumber}: {fr.Message}");
                }
            }

            _txtSummary.Text = sb.ToString();
            AppendStatus(ctx.IsAborted ? "Processing aborted." : "Processing complete.");
        }
        catch (Exception ex)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            AppendStatus($"Processing error: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetProcessingState(running: false);
        }
    }

    // -------------------------------------------------------------------------
    // Abort
    // -------------------------------------------------------------------------

    private void BtnAbort_Click(object sender, EventArgs e)
    {
        _cts?.Cancel();
        _btnAbort.Enabled = false;
        AppendStatus("Abort requested — finishing current row...");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ResetValidationState()
    {
        _validationPassed   = false;
        _btnProcess.Enabled = false;
        _txtStatus.Clear();
        _txtSummary.Clear();
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 0;
    }

    private void SetProcessingState(bool running)
    {
        _btnBrowseSchema.Enabled   = !running;
        _btnBrowseData.Enabled     = !running;
        _btnBrowseOutput.Enabled   = !running;
        _btnTestConnection.Enabled = !running;
        _btnValidate.Enabled       = !running;
        _btnProcess.Enabled        = !running;
        _btnAbort.Enabled          =  running;
    }

    private void AppendStatus(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (_txtStatus.TextLength > 0)
            _txtStatus.AppendText(Environment.NewLine);
        _txtStatus.AppendText(line);
        _txtStatus.SelectionStart = _txtStatus.TextLength;
        _txtStatus.ScrollToCaret();
    }
}

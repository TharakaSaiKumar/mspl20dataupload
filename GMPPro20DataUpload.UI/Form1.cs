using GMPPro20DataUpload.Core.Interfaces;
using GMPPro20DataUpload.Models;
using System.Text;

namespace GMPPro20DataUpload.UI;

public partial class Form1 : Form
{
    private readonly IProcessingService _processingService;
    private readonly IValidationService _validationService;
    private readonly IMongoService _mongoService;
    private readonly IFormatService _formatService;
    private readonly MongoConfiguration _mongoConfig;
    private readonly ApplicationSettings _appSettings;

    private CancellationTokenSource? _cts;
    private bool _validationPassed = false;
    private List<FormatConfiguration> _formats = new();
    private FormatConfiguration? _selectedFormat;

    public Form1(
        IProcessingService processingService,
        IValidationService validationService,
        IMongoService mongoService,
        IFormatService formatService,
        MongoConfiguration mongoConfig,
        ApplicationSettings appSettings)
    {
        _processingService = processingService;
        _validationService = validationService;
        _mongoService      = mongoService;
        _formatService     = formatService;
        _mongoConfig       = mongoConfig;
        _appSettings       = appSettings;
        InitializeComponent();
        LoadFormats();
    }

    // -------------------------------------------------------------------------
    // Format selection
    // -------------------------------------------------------------------------

    private void LoadFormats()
    {
        try
        {
            _formats = _formatService.LoadFormats(_appSettings.FormatsFile);
            foreach (FormatConfiguration fmt in _formats)
                _cboFormat.Items.Add(fmt.DisplayName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load format configurations: {ex.Message}",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CboFormat_SelectedIndexChanged(object sender, EventArgs e)
    {
        _selectedFormat = _cboFormat.SelectedIndex >= 0
            ? _formats[_cboFormat.SelectedIndex]
            : null;
        ResetValidationState();
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
        if (_selectedFormat is null)
        {
            AppendStatus("Please select a format before validating.");
            return;
        }

        string dataPath = _txtDataPath.Text.Trim();

        if (string.IsNullOrEmpty(dataPath))
        {
            AppendStatus("Please select a Data file before validating.");
            return;
        }

        // Validate format configuration
        if (string.IsNullOrWhiteSpace(_selectedFormat.SchemaFile))
        {
            AppendStatus("Selected format has no SchemaFile configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedFormat.TemplateFile))
        {
            AppendStatus("Selected format has no TemplateFile configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedFormat.RequestPrefix))
        {
            AppendStatus("Selected format has no RequestPrefix configured.");
            return;
        }

        string templateFilePath = Path.Combine(_appSettings.TemplateDirectory, _selectedFormat.TemplateFile);
        if (!File.Exists(templateFilePath))
        {
            AppendStatus($"Template file not found: {templateFilePath}");
            return;
        }

        string schemaPath = Path.Combine(_appSettings.TemplateDirectory, _selectedFormat.SchemaFile);

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

        if (_selectedFormat is null)
        {
            AppendStatus("No format selected.");
            return;
        }

        string schemaPath = Path.Combine(_appSettings.TemplateDirectory, _selectedFormat.SchemaFile);
        string dataPath   = _txtDataPath.Text.Trim();
        string outputPath = _txtOutputPath.Text.Trim();

        if (string.IsNullOrEmpty(dataPath))
        {
            AppendStatus("Data file path is required.");
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
                _selectedFormat.ModuleCode,
                _selectedFormat.RequestPrefix,
                _selectedFormat.TemplateFile.Replace(".json",""),
                _mongoConfig,
                progress,
                _cts.Token);

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;

            int insertCount    = ctx.Results.Count(r => string.Equals(r.Status, "Inserted",  StringComparison.OrdinalIgnoreCase));
            int duplicateCount = ctx.Results.Count(r => string.Equals(r.Status, "Duplicate", StringComparison.OrdinalIgnoreCase));
            int failedCount    = ctx.Results.Count(r => !r.IsSuccess);

            var sb = new StringBuilder();
            sb.AppendLine($"Total Rows    : {ctx.TotalRows}");
            sb.AppendLine($"Processed Rows: {ctx.ProcessedRows}");
            sb.AppendLine($"Inserted      : {insertCount}");
            sb.AppendLine($"Duplicate     : {duplicateCount}");
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
        _cboFormat.Enabled             = !running;
        _btnBrowseData.Enabled         = !running;
        _btnBrowseOutput.Enabled       = !running;
        _btnTestConnection.Enabled     = !running;
        _btnValidate.Enabled           = !running;
        _btnProcess.Enabled            = !running;
        _btnAbort.Enabled              =  running;
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

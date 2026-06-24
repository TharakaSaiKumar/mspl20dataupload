namespace GMPPro20DataUpload.UI;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    // File picker rows
    private System.Windows.Forms.Label   _lblFormat;
    private System.Windows.Forms.ComboBox _cboFormat;

    private System.Windows.Forms.Label   _lblData;
    private System.Windows.Forms.TextBox _txtDataPath;
    private System.Windows.Forms.Button  _btnBrowseData;

    private System.Windows.Forms.Label   _lblOutput;
    private System.Windows.Forms.TextBox _txtOutputPath;
    private System.Windows.Forms.Button  _btnBrowseOutput;

    // Action buttons
    private System.Windows.Forms.Button _btnTestConnection;
    private System.Windows.Forms.Button _btnValidate;
    private System.Windows.Forms.Button _btnProcess;
    private System.Windows.Forms.Button _btnAbort;

    // Progress and output areas
    private System.Windows.Forms.ProgressBar _progressBar;
    private System.Windows.Forms.Label       _lblStatus;
    private System.Windows.Forms.TextBox     _txtStatus;
    private System.Windows.Forms.Label       _lblSummary;
    private System.Windows.Forms.TextBox     _txtSummary;

    private void InitializeComponent()
    {
        _lblFormat = new Label();
        _cboFormat = new ComboBox();
        _lblData = new Label();
        _txtDataPath = new TextBox();
        _btnBrowseData = new Button();
        _lblOutput = new Label();
        _txtOutputPath = new TextBox();
        _btnBrowseOutput = new Button();
        _btnTestConnection = new Button();
        _btnValidate = new Button();
        _btnProcess = new Button();
        _btnAbort = new Button();
        _progressBar = new ProgressBar();
        _lblStatus = new Label();
        _txtStatus = new TextBox();
        _lblSummary = new Label();
        _txtSummary = new TextBox();
        SuspendLayout();
        // 
        // _lblFormat
        // 
        _lblFormat.Location = new Point(12, 19);
        _lblFormat.Name = "_lblFormat";
        _lblFormat.Size = new Size(85, 28);
        _lblFormat.TabIndex = 0;
        _lblFormat.Text = "Format:";
        // 
        // _cboFormat
        // 
        _cboFormat.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _cboFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboFormat.Location = new Point(103, 16);
        _cboFormat.Name = "_cboFormat";
        _cboFormat.Size = new Size(762, 31);
        _cboFormat.TabIndex = 1;
        _cboFormat.SelectedIndexChanged += CboFormat_SelectedIndexChanged;
        // 
        // _lblData
        // 
        _lblData.Location = new Point(12, 76);
        _lblData.Name = "_lblData";
        _lblData.Size = new Size(85, 28);
        _lblData.TabIndex = 3;
        _lblData.Text = "Data File:";
        // 
        // _txtDataPath
        // 
        _txtDataPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtDataPath.Location = new Point(103, 73);
        _txtDataPath.Name = "_txtDataPath";
        _txtDataPath.ReadOnly = true;
        _txtDataPath.Size = new Size(671, 31);
        _txtDataPath.TabIndex = 4;
        // 
        // _btnBrowseData
        // 
        _btnBrowseData.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnBrowseData.Location = new Point(784, 72);
        _btnBrowseData.Name = "_btnBrowseData";
        _btnBrowseData.Size = new Size(90, 35);
        _btnBrowseData.TabIndex = 5;
        _btnBrowseData.Text = "Browse…";
        _btnBrowseData.Click += BtnBrowseData_Click;
        // 
        // _lblOutput
        // 
        _lblOutput.Location = new Point(12, 128);
        _lblOutput.Name = "_lblOutput";
        _lblOutput.Size = new Size(85, 28);
        _lblOutput.TabIndex = 6;
        _lblOutput.Text = "Output File:";
        // 
        // _txtOutputPath
        // 
        _txtOutputPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtOutputPath.Location = new Point(103, 125);
        _txtOutputPath.Name = "_txtOutputPath";
        _txtOutputPath.ReadOnly = true;
        _txtOutputPath.Size = new Size(671, 31);
        _txtOutputPath.TabIndex = 7;
        // 
        // _btnBrowseOutput
        // 
        _btnBrowseOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnBrowseOutput.Location = new Point(784, 124);
        _btnBrowseOutput.Name = "_btnBrowseOutput";
        _btnBrowseOutput.Size = new Size(90, 35);
        _btnBrowseOutput.TabIndex = 8;
        _btnBrowseOutput.Text = "Browse…";
        _btnBrowseOutput.Click += BtnBrowseOutput_Click;
        // 
        // _btnTestConnection
        // 
        _btnTestConnection.Location = new Point(12, 173);
        _btnTestConnection.Name = "_btnTestConnection";
        _btnTestConnection.Size = new Size(175, 36);
        _btnTestConnection.TabIndex = 9;
        _btnTestConnection.Text = "Test MongoDB Connection";
        _btnTestConnection.Click += BtnTestConnection_Click;
        // 
        // _btnValidate
        // 
        _btnValidate.Location = new Point(197, 173);
        _btnValidate.Name = "_btnValidate";
        _btnValidate.Size = new Size(90, 36);
        _btnValidate.TabIndex = 10;
        _btnValidate.Text = "Validate";
        _btnValidate.Click += BtnValidate_Click;
        // 
        // _btnProcess
        // 
        _btnProcess.Enabled = false;
        _btnProcess.Location = new Point(297, 173);
        _btnProcess.Name = "_btnProcess";
        _btnProcess.Size = new Size(90, 36);
        _btnProcess.TabIndex = 11;
        _btnProcess.Text = "Process";
        _btnProcess.Click += BtnProcess_Click;
        // 
        // _btnAbort
        // 
        _btnAbort.Enabled = false;
        _btnAbort.Location = new Point(397, 173);
        _btnAbort.Name = "_btnAbort";
        _btnAbort.Size = new Size(90, 36);
        _btnAbort.TabIndex = 12;
        _btnAbort.Text = "Abort";
        _btnAbort.Click += BtnAbort_Click;
        // 
        // _progressBar
        // 
        _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progressBar.Location = new Point(12, 228);
        _progressBar.Name = "_progressBar";
        _progressBar.Size = new Size(867, 23);
        _progressBar.TabIndex = 13;
        // 
        // _lblStatus
        // 
        _lblStatus.Location = new Point(12, 273);
        _lblStatus.Name = "_lblStatus";
        _lblStatus.Size = new Size(60, 28);
        _lblStatus.TabIndex = 14;
        _lblStatus.Text = "Status:";
        // 
        // _txtStatus
        // 
        _txtStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _txtStatus.BackColor = SystemColors.Window;
        _txtStatus.Location = new Point(12, 304);
        _txtStatus.Multiline = true;
        _txtStatus.Name = "_txtStatus";
        _txtStatus.ReadOnly = true;
        _txtStatus.ScrollBars = ScrollBars.Vertical;
        _txtStatus.Size = new Size(867, 296);
        _txtStatus.TabIndex = 15;
        _txtStatus.Font = new Font("Consolas", 10F);
        // 
        // _lblSummary
        // 
        _lblSummary.Location = new Point(12, 609);
        _lblSummary.Name = "_lblSummary";
        _lblSummary.Size = new Size(125, 29);
        _lblSummary.TabIndex = 16;
        _lblSummary.Text = "Summary:";
        // 
        // _txtSummary
        // 
        _txtSummary.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _txtSummary.BackColor = SystemColors.Window;
        _txtSummary.Location = new Point(12, 637);
        _txtSummary.Multiline = true;
        _txtSummary.Name = "_txtSummary";
        _txtSummary.ReadOnly = true;
        _txtSummary.ScrollBars = ScrollBars.Vertical;
        _txtSummary.Size = new Size(867, 276);
        _txtSummary.TabIndex = 17;
        _txtSummary.Font = new Font("Consolas", 10F);
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(891, 921);
        Controls.Add(_lblFormat);
        Controls.Add(_cboFormat);
        Controls.Add(_lblData);
        Controls.Add(_txtDataPath);
        Controls.Add(_btnBrowseData);
        Controls.Add(_lblOutput);
        Controls.Add(_txtOutputPath);
        Controls.Add(_btnBrowseOutput);
        Controls.Add(_btnTestConnection);
        Controls.Add(_btnValidate);
        Controls.Add(_btnProcess);
        Controls.Add(_btnAbort);
        Controls.Add(_progressBar);
        Controls.Add(_lblStatus);
        Controls.Add(_txtStatus);
        Controls.Add(_lblSummary);
        Controls.Add(_txtSummary);
        MinimumSize = new Size(740, 570);
        Name = "Form1";
        Text = "GMP Pro 2.0 Data Upload";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion
}

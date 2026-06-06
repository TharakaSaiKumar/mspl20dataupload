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
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    // TODO: TEMP — remove Run Core Test button after developer testing is complete
    private System.Windows.Forms.Button _btnRunCoreTest;

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(800, 450);
        this.Text = "Form1";

        // TODO: TEMP — developer test button
        this._btnRunCoreTest = new System.Windows.Forms.Button();
        this._btnRunCoreTest.Text = "Run Core Test";
        this._btnRunCoreTest.Size = new System.Drawing.Size(150, 40);
        this._btnRunCoreTest.Location = new System.Drawing.Point(20, 20);
        this._btnRunCoreTest.Click += new System.EventHandler(this.BtnRunCoreTest_Click);
        this.Controls.Add(this._btnRunCoreTest);
    }

    #endregion
}

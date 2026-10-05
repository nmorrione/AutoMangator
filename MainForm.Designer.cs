namespace AutoMangator;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        lblUrls = new Label();
        txtUrls = new TextBox();
        lblName = new Label();
        txtName = new TextBox();
        lblFolder = new Label();
        txtFolder = new TextBox();
        btnBrowse = new Button();
        lblMin = new Label();
        numMin = new NumericUpDown();
        chkShow = new CheckBox();
        btnStart = new Button();
        btnCancel = new Button();
        btnOpenFolder = new Button();
        progress = new ProgressBar();
        txtLog = new TextBox();
        ((System.ComponentModel.ISupportInitialize)numMin).BeginInit();
        SuspendLayout();
        //
        // lblUrls
        //
        lblUrls.AutoSize = true;
        lblUrls.Location = new Point(12, 12);
        lblUrls.Name = "lblUrls";
        lblUrls.Size = new Size(150, 15);
        lblUrls.TabIndex = 0;
        lblUrls.Text = "Indirizzi (uno per riga):";
        //
        // txtUrls
        //
        txtUrls.AcceptsReturn = true;
        txtUrls.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtUrls.Location = new Point(12, 32);
        txtUrls.Multiline = true;
        txtUrls.Name = "txtUrls";
        txtUrls.ScrollBars = ScrollBars.Vertical;
        txtUrls.Size = new Size(736, 110);
        txtUrls.TabIndex = 1;
        txtUrls.WordWrap = false;
        //
        // lblName
        //
        lblName.AutoSize = true;
        lblName.Location = new Point(12, 154);
        lblName.Name = "lblName";
        lblName.Size = new Size(420, 15);
        lblName.TabIndex = 2;
        lblName.Text = "Nome file (opzionale; vuoto = titolo della pagina; con più indirizzi viene numerato):";
        //
        // txtName
        //
        txtName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtName.Location = new Point(12, 174);
        txtName.Name = "txtName";
        txtName.Size = new Size(736, 23);
        txtName.TabIndex = 3;
        //
        // lblFolder
        //
        lblFolder.AutoSize = true;
        lblFolder.Location = new Point(12, 206);
        lblFolder.Name = "lblFolder";
        lblFolder.Size = new Size(140, 15);
        lblFolder.TabIndex = 4;
        lblFolder.Text = "Cartella di destinazione:";
        //
        // txtFolder
        //
        txtFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtFolder.Location = new Point(12, 226);
        txtFolder.Name = "txtFolder";
        txtFolder.Size = new Size(650, 23);
        txtFolder.TabIndex = 5;
        //
        // btnBrowse
        //
        btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowse.Location = new Point(668, 225);
        btnBrowse.Name = "btnBrowse";
        btnBrowse.Size = new Size(80, 25);
        btnBrowse.TabIndex = 6;
        btnBrowse.Text = "Sfoglia...";
        btnBrowse.UseVisualStyleBackColor = true;
        btnBrowse.Click += btnBrowse_Click;
        //
        // lblMin
        //
        lblMin.AutoSize = true;
        lblMin.Location = new Point(12, 264);
        lblMin.Name = "lblMin";
        lblMin.Size = new Size(160, 15);
        lblMin.TabIndex = 7;
        lblMin.Text = "Lato minimo immagine (px):";
        //
        // numMin
        //
        numMin.Increment = new decimal(new int[] { 50, 0, 0, 0 });
        numMin.Location = new Point(180, 261);
        numMin.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
        numMin.Name = "numMin";
        numMin.Size = new Size(80, 23);
        numMin.TabIndex = 8;
        numMin.Value = new decimal(new int[] { 300, 0, 0, 0 });
        //
        // chkShow
        //
        chkShow.AutoSize = true;
        chkShow.Location = new Point(290, 263);
        chkShow.Name = "chkShow";
        chkShow.Size = new Size(380, 19);
        chkShow.TabIndex = 9;
        chkShow.Text = "Mostra il browser (se il sito blocca o chiede una verifica)";
        chkShow.UseVisualStyleBackColor = true;
        //
        // btnStart
        //
        btnStart.Location = new Point(12, 298);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(120, 30);
        btnStart.TabIndex = 10;
        btnStart.Text = "Crea CBZ";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += btnStart_Click;
        //
        // btnCancel
        //
        btnCancel.Enabled = false;
        btnCancel.Location = new Point(140, 298);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(100, 30);
        btnCancel.TabIndex = 11;
        btnCancel.Text = "Annulla";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += btnCancel_Click;
        //
        // btnOpenFolder
        //
        btnOpenFolder.Location = new Point(248, 298);
        btnOpenFolder.Name = "btnOpenFolder";
        btnOpenFolder.Size = new Size(130, 30);
        btnOpenFolder.TabIndex = 12;
        btnOpenFolder.Text = "Apri cartella";
        btnOpenFolder.UseVisualStyleBackColor = true;
        btnOpenFolder.Click += btnOpenFolder_Click;
        //
        // progress
        //
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progress.Location = new Point(12, 338);
        progress.MarqueeAnimationSpeed = 30;
        progress.Name = "progress";
        progress.Size = new Size(736, 14);
        progress.TabIndex = 13;
        //
        // txtLog
        //
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.Font = new Font("Consolas", 9F);
        txtLog.Location = new Point(12, 362);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Both;
        txtLog.Size = new Size(736, 226);
        txtLog.TabIndex = 14;
        txtLog.WordWrap = false;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(760, 600);
        Controls.Add(lblUrls);
        Controls.Add(txtUrls);
        Controls.Add(lblName);
        Controls.Add(txtName);
        Controls.Add(lblFolder);
        Controls.Add(txtFolder);
        Controls.Add(btnBrowse);
        Controls.Add(lblMin);
        Controls.Add(numMin);
        Controls.Add(chkShow);
        Controls.Add(btnStart);
        Controls.Add(btnCancel);
        Controls.Add(btnOpenFolder);
        Controls.Add(progress);
        Controls.Add(txtLog);
        MinimumSize = new Size(600, 500);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AutoMangator";
        ((System.ComponentModel.ISupportInitialize)numMin).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label lblUrls;
    private TextBox txtUrls;
    private Label lblName;
    private TextBox txtName;
    private Label lblFolder;
    private TextBox txtFolder;
    private Button btnBrowse;
    private Label lblMin;
    private NumericUpDown numMin;
    private CheckBox chkShow;
    private Button btnStart;
    private Button btnCancel;
    private Button btnOpenFolder;
    private ProgressBar progress;
    private TextBox txtLog;
}

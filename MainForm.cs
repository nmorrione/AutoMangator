using System.Diagnostics;

namespace AutoMangator;

public partial class MainForm : Form
{
    private CancellationTokenSource? _cts;
    private string? _lastFile;

    public MainForm()
    {
        InitializeComponent();
        txtFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { InitialDirectory = txtFolder.Text, UseDescriptionForTitle = true, Description = "Cartella dove salvare i file .cbz" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtFolder.Text = dlg.SelectedPath;
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        if (_lastFile is not null && File.Exists(_lastFile))
            Process.Start("explorer.exe", $"/select,\"{_lastFile}\"");
        else if (Directory.Exists(txtFolder.Text))
            Process.Start("explorer.exe", $"\"{txtFolder.Text}\"");
    }

    private void btnCancel_Click(object sender, EventArgs e) => _cts?.Cancel();

    private async void btnStart_Click(object sender, EventArgs e)
    {
        var urls = txtUrls.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var invalid = urls.FirstOrDefault(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"));
        if (urls.Count == 0 || invalid is not null)
        {
            MessageBox.Show(this, invalid is null ? "Inserisci almeno un indirizzo." : $"Indirizzo non valido:\n{invalid}",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string folder = txtFolder.Text.Trim();
        string? baseName = string.IsNullOrWhiteSpace(txtName.Text) ? null : txtName.Text.Trim();
        int minSide = (int)numMin.Value;

        SetRunning(true);
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(Log);
        int ok = 0;
        try
        {
            await using var maker = await CbzMaker.StartAsync(chkShow.Checked);
            for (int i = 0; i < urls.Count; i++)
            {
                // Con più indirizzi il nome scelto riceve un numero progressivo
                string? name = baseName is null ? null : urls.Count == 1 ? baseName : $"{baseName} {i + 1:D2}";
                try
                {
                    _lastFile = await maker.CreateAsync(urls[i], folder, name, minSide, log, _cts.Token);
                    Log($"Creato {_lastFile}");
                    ok++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log($"ERRORE: {ex.Message}");
                }
                Log("");
            }
            Log($"Fatto: {ok} di {urls.Count} file creati.");
        }
        catch (OperationCanceledException)
        {
            Log("Operazione annullata.");
        }
        catch (Exception ex)
        {
            Log($"ERRORE: {ex.Message}");
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    private void SetRunning(bool running)
    {
        btnStart.Enabled = !running;
        btnCancel.Enabled = running;
        txtUrls.ReadOnly = txtName.ReadOnly = txtFolder.ReadOnly = running;
        btnBrowse.Enabled = numMin.Enabled = chkShow.Enabled = !running;
        progress.Style = running ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        UseWaitCursor = running;
    }

    private void Log(string line) => txtLog.AppendText(line + Environment.NewLine);

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}

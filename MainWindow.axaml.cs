using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AutoMangator;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private string? _lastFile;

    public MainWindow()
    {
        InitializeComponent();
        txtFolder.Text = Program.DefaultOutputFolder;
    }

    private async void btnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(txtFolder.Text) ? await StorageProvider.TryGetFolderFromPathAsync(txtFolder.Text) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Cartella dove salvare i file .cbz",
            SuggestedStartLocation = start,
        });
        if (folders is [var folder] && folder.TryGetLocalPath() is { } path)
            txtFolder.Text = path;
    }

    private void btnOpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastFile is not null && File.Exists(_lastFile))
            OpenInFileManager(_lastFile);
        else if (Directory.Exists(txtFolder.Text?.Trim()))
            OpenInFileManager(txtFolder.Text.Trim());
    }

    private async void btnKobo_Click(object? sender, RoutedEventArgs e)
    {
        // Propone la cartella dell'ultimo .cbz creato, se c'è
        string? folder = _lastFile is not null ? Path.GetDirectoryName(_lastFile) : null;
        await new KoboWindow(folder).ShowDialog(this);
    }

    /// <summary>Apre una cartella, o la cartella di un file selezionandolo, in Esplora file / Finder.</summary>
    public static void OpenInFileManager(string path)
    {
        bool isFile = File.Exists(path);
        if (OperatingSystem.IsWindows())
            Process.Start("explorer.exe", isFile ? $"/select,\"{path}\"" : $"\"{path}\"");
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", isFile ? ["-R", path] : [path]);
        else
            Process.Start("xdg-open", isFile ? Path.GetDirectoryName(path)! : path);
    }

    private void btnCancel_Click(object? sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void btnStart_Click(object? sender, RoutedEventArgs e)
    {
        var urls = (txtUrls.Text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var invalid = urls.FirstOrDefault(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"));
        if (urls.Count == 0 || invalid is not null)
        {
            Log(invalid is null ? "Inserisci almeno un indirizzo." : $"Indirizzo non valido: {invalid}");
            return;
        }

        string folder = (txtFolder.Text ?? "").Trim();
        string? baseName = string.IsNullOrWhiteSpace(txtName.Text) ? null : txtName.Text.Trim();
        int minSide = (int)(numMin.Value ?? 300);

        SetRunning(true);
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(Log);
        int ok = 0;
        try
        {
            await using var maker = await CbzMaker.StartAsync(chkShow.IsChecked == true, log);
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
        btnStart.IsEnabled = !running;
        btnCancel.IsEnabled = running;
        txtUrls.IsReadOnly = txtName.IsReadOnly = txtFolder.IsReadOnly = running;
        btnBrowse.IsEnabled = numMin.IsEnabled = chkShow.IsEnabled = btnKobo.IsEnabled = !running;
        progress.IsIndeterminate = running;
    }

    private void Log(string line)
    {
        txtLog.Text += line + Environment.NewLine;
        txtLog.CaretIndex = txtLog.Text.Length;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnClosing(e);
    }
}

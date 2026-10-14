using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using static Web2Comic.Loc;

namespace Web2Comic;

/// <summary>Chiede i dati della serie e crea un EPUB da ogni .cbz di una cartella.</summary>
public partial class ConvertWindow : Window
{
    private List<ChapterInfo> _chapters = [];
    private bool _running;

    public ConvertWindow() : this(null) { }

    public ConvertWindow(string? folder)
    {
        InitializeComponent();
        if (folder is not null && Directory.Exists(folder))
            LoadFolder(folder);
    }

    private async void btnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(txtFolder.Text) ? await StorageProvider.TryGetFolderFromPathAsync(txtFolder.Text) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = T("Convert.FolderPicker"),
            SuggestedStartLocation = start,
        });
        if (folders is [var f] && f.TryGetLocalPath() is { } path)
            LoadFolder(path);
    }

    private void LoadFolder(string folder)
    {
        txtFolder.Text = folder;
        _chapters = EpubMaker.LoadChapters(folder);
        lstChapters.ItemsSource = _chapters;
        txtLog.Text = "";

        // Se gli EPUB erano già stati creati, ripropone i dati usati l'ultima volta
        if (EpubMaker.TryReadExisting(folder) is { } info)
        {
            series.Fill(info);
            Log(T("Convert.Restored"));
        }
        else if (series.Series.Length == 0)
        {
            series.Series = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        Log(_chapters.Count == 0 ? T("Convert.NoCbz") : T("Convert.Found", _chapters.Count));
    }

    private async void btnCreate_Click(object? sender, RoutedEventArgs e)
    {
        var book = series.Read();
        if (_chapters.Count == 0) { Log(T("Convert.ChooseFolder")); return; }
        if (book.Series.Length == 0) { Log(T("Log.SeriesRequired")); return; }
        var dup = _chapters.GroupBy(c => c.Number.Trim()).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) { Log(T("Convert.Duplicate", dup.Key)); return; }

        string output = Path.Combine(txtFolder.Text!, EpubMaker.OutputFolderName);
        SetRunning(true);
        var log = new Progress<string>(Log);
        int ok = 0;
        // Fuori dal thread dell'interfaccia: la finestra resta reattiva
        await Task.Run(() =>
        {
            foreach (var ch in _chapters)
            {
                try
                {
                    string file = EpubMaker.Create(book, ch, output);
                    ((IProgress<string>)log).Report($"  {Path.GetFileName(file)}");
                    ok++;
                }
                catch (Exception ex)
                {
                    ((IProgress<string>)log).Report(T("Convert.ErrorIn", ch.FileName, ex.Message));
                }
            }
        });
        Log(T("Convert.Done", ok, _chapters.Count, output));
        if (ok > 0) Log(T("Convert.CopyHint"));
        SetRunning(false);
    }

    private void btnOpen_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtFolder.Text)) return;
        string epubs = Path.Combine(txtFolder.Text, EpubMaker.OutputFolderName);
        MainWindow.OpenInFileManager(Directory.Exists(epubs) ? epubs : txtFolder.Text);
    }

    private void btnClose_Click(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Non chiudere a metà scrittura
        if (_running) e.Cancel = true;
        base.OnClosing(e);
    }

    private void SetRunning(bool running)
    {
        _running = running;
        btnCreate.IsEnabled = btnBrowse.IsEnabled = btnClose.IsEnabled = !running;
        progress.IsIndeterminate = running;
    }

    private void Log(string line)
    {
        txtLog.Text += line + Environment.NewLine;
        txtLog.CaretIndex = txtLog.Text.Length;
    }
}

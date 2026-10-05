using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AutoMangator;

/// <summary>Chiede i dati della serie e crea un EPUB per Kobo da ogni .cbz di una cartella.</summary>
public partial class KoboWindow : Window
{
    private List<ChapterInfo> _chapters = [];
    private bool _running;

    public KoboWindow() : this(null) { }

    public KoboWindow(string? folder)
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
            Title = "Cartella con i file .cbz dei capitoli",
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
            Fill(info);
            Log("Dati della serie ripresi dagli EPUB già creati in questa cartella.");
        }
        else if (string.IsNullOrWhiteSpace(txtSeries.Text))
        {
            txtSeries.Text = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        Log(_chapters.Count == 0 ? "Nessun file .cbz in questa cartella." : $"Trovati {_chapters.Count} capitoli.");
    }

    private void Fill(BookInfo b)
    {
        txtSeries.Text = b.Series;
        txtWriter.Text = b.Writer;
        txtArtist.Text = b.Artist;
        txtPublisher.Text = b.Publisher;
        txtYear.Text = b.Year;
        txtGenre.Text = b.Genre;
        txtLanguage.Text = b.Language;
        txtSummary.Text = b.Summary;
        chkRtl.IsChecked = b.RightToLeft;
    }

    private BookInfo Read() => new()
    {
        Series = (txtSeries.Text ?? "").Trim(),
        Writer = txtWriter.Text ?? "",
        Artist = txtArtist.Text ?? "",
        Publisher = txtPublisher.Text ?? "",
        Year = txtYear.Text ?? "",
        Genre = txtGenre.Text ?? "",
        Language = txtLanguage.Text ?? "",
        Summary = txtSummary.Text ?? "",
        RightToLeft = chkRtl.IsChecked == true,
    };

    private async void btnCreate_Click(object? sender, RoutedEventArgs e)
    {
        var book = Read();
        if (_chapters.Count == 0) { Log("Scegli prima una cartella con dei file .cbz."); return; }
        if (book.Series.Length == 0) { Log("Il nome della serie è obbligatorio."); return; }
        var dup = _chapters.GroupBy(c => c.Number.Trim()).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) { Log($"Il numero {dup.Key} è usato da più capitoli: correggilo prima di continuare."); return; }

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
                    ((IProgress<string>)log).Report($"ERRORE in {ch.FileName}: {ex.Message}");
                }
            }
        });
        Log($"Fatto: {ok} di {_chapters.Count} EPUB creati in {output}");
        if (ok > 0) Log("Copiali nel Kobo collegato via USB (anche in una sottocartella).");
        SetRunning(false);
    }

    private void btnOpen_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(txtFolder.Text)) return;
        string kobo = Path.Combine(txtFolder.Text, EpubMaker.OutputFolderName);
        MainWindow.OpenInFileManager(Directory.Exists(kobo) ? kobo : txtFolder.Text);
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

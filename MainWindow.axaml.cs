using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AutoMangator;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private string? _lastFile;
    private double _epubGrowth; // di quanto è stata allungata la finestra per il pannello EPUB

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

    private void chkEpub_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        bool on = chkEpub.IsChecked == true;
        if (pnlEpub.IsVisible == on) return;
        pnlEpub.IsVisible = on;
        btnStart.Content = on ? "Crea EPUB" : "Crea CBZ";

        // Allunga la finestra quanto il pannello (senza superare lo schermo) e,
        // togliendo la spunta, la riaccorcia di quanto era stata allungata
        if (on)
        {
            pnlEpub.Measure(new Size(Math.Max(0, Bounds.Width - 24), double.PositiveInfinity));
            double before = Height;
            Height += pnlEpub.DesiredSize.Height + 4;
            if (Screens.ScreenFromWindow(this) is { } screen)
            {
                // Resta dentro l'area utile dello schermo (barra delle applicazioni / Dock esclusi),
                // spostando la finestra più in alto se il fondo finirebbe fuori
                var area = screen.WorkingArea;
                const int frame = 40; // barra del titolo e bordi, in pixel logici
                Height = Math.Min(Height, Math.Max(before, area.Height / screen.Scaling - frame));
                int bottom = Position.Y + (int)((Height + frame) * screen.Scaling);
                if (bottom > area.Bottom)
                    Position = new PixelPoint(Position.X, Math.Max(area.Y, Position.Y - (bottom - area.Bottom)));
            }
            _epubGrowth = Height - before;
        }
        else
        {
            Height = Math.Max(MinHeight, Height - _epubGrowth);
            _epubGrowth = 0;
        }
    }

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

        // Opzioni EPUB: dati della serie e numero del primo capitolo
        BookInfo? book = null;
        double? firstChapter = null;
        bool keepCbz = chkKeepCbz.IsChecked == true;
        if (chkEpub.IsChecked == true)
        {
            book = series.Read();
            if (book.Series.Length == 0) { Log("Per creare gli EPUB serve il nome della serie."); return; }
            string start = (txtChapter.Text ?? "").Trim().Replace(',', '.');
            if (start.Length > 0)
            {
                if (!double.TryParse(start, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                {
                    Log($"Numero capitolo non valido: {txtChapter.Text}");
                    return;
                }
                firstChapter = n;
            }
        }

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
                    if (book is null)
                    {
                        Log($"Creato {_lastFile}");
                    }
                    else
                    {
                        // Numero: quello indicato (+1 per ogni indirizzo), altrimenti dal titolo della pagina
                        string number = firstChapter is { } first
                            ? (first + i).ToString(CultureInfo.InvariantCulture)
                            : EpubMaker.NumberFromName(Path.GetFileNameWithoutExtension(_lastFile)) ?? (i + 1).ToString(CultureInfo.InvariantCulture);
                        var chapter = new ChapterInfo { CbzPath = _lastFile, Number = number };
                        string cbz = _lastFile;
                        _lastFile = await Task.Run(() => EpubMaker.Create(book, chapter, folder));
                        Log($"Capitolo {number}: creato {_lastFile}");
                        if (keepCbz) Log($"Conservato {cbz}");
                        else File.Delete(cbz);
                    }
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
        chkEpub.IsEnabled = pnlEpub.IsEnabled = !running;
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

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace AutoMangator;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private string? _lastFile;
    private double _epubGrowth; // di quanto è stata allungata la finestra per il pannello EPUB
    // Capitoli trovati con "Trova capitoli": numero e titolo noti per indirizzo
    private Dictionary<string, FoundChapter> _found = [];

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
        UpdateStartLabel();

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

    private void chkVolume_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        pnlVolumeNumber.IsVisible = chkVolume.IsChecked == true;
        UpdateStartLabel();
    }

    // "Crea CBZ", "Crea EPUB", "Crea volume CBZ", "Crea volume EPUB"
    private void UpdateStartLabel() =>
        btnStart.Content = (chkVolume.IsChecked == true ? "Crea volume " : "Crea ") + (chkEpub.IsChecked == true ? "EPUB" : "CBZ");

    // ---------- Lista capitoli ----------

    private async void btnFind_Click(object? sender, RoutedEventArgs e)
    {
        string listUrl = (txtListUrl.Text ?? "").Trim();
        if (!Uri.TryCreate(listUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            Log("Incolla prima l'indirizzo della pagina con la lista dei capitoli.");
            return;
        }
        if (!TryParseOptional(txtFrom.Text, out double? from) || !TryParseOptional(txtTo.Text, out double? to))
        {
            Log("\"dal\" e \"al\" devono essere numeri di capitolo (o vuoti).");
            return;
        }

        SetRunning(true);
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(Log);
        try
        {
            await using var maker = await CbzMaker.StartAsync(chkShow.IsChecked == true, log);
            var all = await maker.FindChaptersAsync(listUrl, log, _cts.Token);
            if (all.Count == 0)
            {
                Log("Nessun capitolo riconosciuto in questa pagina: incolla gli indirizzi dei capitoli a mano.");
                return;
            }

            var chosen = all.Where(c => (from is null || c.Number >= from) && (to is null || c.Number <= to)).ToList();
            Log($"Trovati {all.Count} capitoli nella lista (dal {Format(all[0].Number)} al {Format(all[^1].Number)}).");
            if (chosen.Count == 0)
            {
                Log("Nessun capitolo nell'intervallo scelto: controlla \"dal\" e \"al\".");
                return;
            }

            _found = chosen.ToDictionary(c => c.Url);
            txtUrls.Text = string.Join(Environment.NewLine, chosen.Select(c => c.Url));
            Log($"Inseriti in \"Indirizzi\" {chosen.Count} capitoli (dal {Format(chosen[0].Number)} al {Format(chosen[^1].Number)}): " +
                $"controllali e poi premi \"{btnStart.Content}\".");
            Log("");
        }
        catch (SecurityCheckException ex)
        {
            Log($"⚠ {ex.Message}");
            chkShow.IsChecked = true;
            Log("⚠ Ho già spuntato \"Mostra il browser\": premi di nuovo \"Trova capitoli\" per riprovare.");
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

    // ---------- Creazione ----------

    private async void btnStart_Click(object? sender, RoutedEventArgs e)
    {
        var urls = (txtUrls.Text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var invalid = urls.FirstOrDefault(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"));
        if (urls.Count == 0 || invalid is not null)
        {
            Log(invalid is null ? "Inserisci almeno un indirizzo." : $"Indirizzo non valido: {invalid}");
            return;
        }

        var job = new Job
        {
            Urls = urls,
            Folder = (txtFolder.Text ?? "").Trim(),
            BaseName = string.IsNullOrWhiteSpace(txtName.Text) ? null : txtName.Text.Trim(),
            MinSide = (int)(numMin.Value ?? 300),
            ForceReader = chkReader.IsChecked == true,
            KeepCbz = chkKeepCbz.IsChecked == true,
            Volume = chkVolume.IsChecked == true,
            VolumeNumber = string.IsNullOrWhiteSpace(txtVolume.Text) ? null : txtVolume.Text.Trim(),
        };

        // Opzioni EPUB: dati della serie e numero del primo capitolo
        if (chkEpub.IsChecked == true)
        {
            job.Book = series.Read();
            if (job.Book.Series.Length == 0) { Log("Per creare gli EPUB serve il nome della serie."); return; }
            if (!TryParseOptional(txtChapter.Text, out job.FirstChapter))
            {
                Log($"Numero capitolo non valido: {txtChapter.Text}");
                return;
            }
        }

        SetRunning(true);
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(Log);
        bool antiBot = false;
        try
        {
            await using var maker = await CbzMaker.StartAsync(chkShow.IsChecked == true, log);
            antiBot = job.Volume
                ? await CreateVolumeAsync(maker, job, log, _cts.Token)
                : await CreateChaptersAsync(maker, job, log, _cts.Token);
            if (antiBot)
            {
                // Basta premere di nuovo il pulsante: il browser visibile è già selezionato
                chkShow.IsChecked = true;
                Log($"⚠ Ho già spuntato \"Mostra il browser\": premi di nuovo \"{btnStart.Content}\" per riprovare.");
            }
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

    /// <summary>Opzioni di una creazione, lette dalla finestra prima di iniziare.</summary>
    private sealed class Job
    {
        public required List<string> Urls { get; init; }
        public required string Folder { get; init; }
        public string? BaseName { get; init; }
        public int MinSide { get; init; }
        public bool ForceReader { get; init; }
        public bool KeepCbz { get; init; }
        public bool Volume { get; init; }
        public string? VolumeNumber { get; init; }
        public BookInfo? Book { get; set; }
        public double? FirstChapter;
    }

    // Un file per capitolo (.cbz, oppure .kepub.epub). Restituisce true se un sito ha chiesto la verifica anti-bot.
    private async Task<bool> CreateChaptersAsync(CbzMaker maker, Job job, IProgress<string> log, CancellationToken ct)
    {
        int ok = 0;
        bool antiBot = false;
        for (int i = 0; i < job.Urls.Count; i++)
        {
            // Con più indirizzi il nome scelto riceve un numero progressivo
            string? name = job.BaseName is null ? null : job.Urls.Count == 1 ? job.BaseName : $"{job.BaseName} {i + 1:D2}";
            try
            {
                _lastFile = await maker.CreateAsync(job.Urls[i], job.Folder, name, job.MinSide, log, ct, job.ForceReader);
                if (job.Book is not { } book)
                {
                    Log($"Creato {_lastFile}");
                }
                else
                {
                    var chapter = Chapter(job, i, _lastFile);
                    string cbz = _lastFile;
                    _lastFile = await Task.Run(() => EpubMaker.Create(book, chapter, job.Folder));
                    Log($"Capitolo {chapter.Number}: creato {_lastFile}");
                    if (job.KeepCbz) Log($"Conservato {cbz}");
                    else File.Delete(cbz);
                }
                ok++;
            }
            catch (SecurityCheckException ex)
            {
                Log($"⚠ {ex.Message}");
                antiBot = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"ERRORE: {ex.Message}");
            }
            Log("");
        }
        Log($"Fatto: {ok} di {job.Urls.Count} file creati.");
        return antiBot;
    }

    // Tutti i capitoli in un unico file. Ogni capitolo viene prima scaricato in una cartella temporanea.
    private async Task<bool> CreateVolumeAsync(CbzMaker maker, Job job, IProgress<string> log, CancellationToken ct)
    {
        string temp = Path.Combine(Path.GetTempPath(), "AutoMangator", Guid.NewGuid().ToString("N"));
        var chapters = new List<ChapterInfo>();
        var failed = new List<string>();
        bool antiBot = false;
        try
        {
            for (int i = 0; i < job.Urls.Count; i++)
            {
                Log($"Capitolo {i + 1} di {job.Urls.Count}");
                try
                {
                    string cbz = await maker.CreateAsync(job.Urls[i], temp, null, job.MinSide, log, ct, job.ForceReader);
                    chapters.Add(Chapter(job, i, cbz));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log(ex is SecurityCheckException ? $"⚠ {ex.Message}" : $"ERRORE: {ex.Message}");
                    antiBot |= ex is SecurityCheckException;
                    failed.Add(_found.TryGetValue(job.Urls[i], out var f) ? $"Capitolo {Format(f.Number)}" : job.Urls[i]);
                }
                Log("");
            }

            if (chapters.Count == 0)
            {
                Log("Nessun capitolo scaricato: volume non creato.");
                return antiBot;
            }
            if (failed.Count > 0)
            {
                string list = string.Join("\n", failed.Take(10).Select(f => "• " + f)) + (failed.Count > 10 ? $"\n• ... e altri {failed.Count - 10}" : "");
                string missing = failed.Count == 1
                    ? $"1 capitolo su {job.Urls.Count} non è stato scaricato"
                    : $"{failed.Count} capitoli su {job.Urls.Count} non sono stati scaricati";
                string others = chapters.Count == 1 ? "l'unico capitolo scaricato" : $"gli altri {chapters.Count} capitoli";
                bool go = await ConfirmAsync("Capitoli non scaricati",
                    $"{missing}:\n\n{list}\n\nVuoi creare comunque il volume con {others}?",
                    "Crea comunque", "Annulla");
                if (!go)
                {
                    Log("Volume non creato.");
                    return antiBot;
                }
                Log($"Creo il volume senza: {string.Join(", ", failed)}.");
            }

            Log($"Unisco {chapters.Count} capitoli in un volume...");
            if (job.Book is { } book)
            {
                _lastFile = await Task.Run(() => EpubMaker.CreateVolume(book, chapters, job.VolumeNumber, job.Folder, job.BaseName));
                Log($"Volume creato: {_lastFile}");
                if (job.KeepCbz)
                    Log($"Conservato {await Task.Run(() => CbzMaker.MergeCbz(chapters.Select(c => c.CbzPath), job.Folder, Path.GetFileName(_lastFile).Replace(".kepub.epub", "")))}");
            }
            else
            {
                string name = job.BaseName ?? $"{SeriesFromTitle(Path.GetFileNameWithoutExtension(chapters[0].CbzPath))} - Capitoli " +
                    $"{EpubMaker.PadNumber(chapters[0].Number)}-{EpubMaker.PadNumber(chapters[^1].Number)}";
                _lastFile = await Task.Run(() => CbzMaker.MergeCbz(chapters.Select(c => c.CbzPath), job.Folder, name));
                Log($"Volume creato: {_lastFile}");
            }
            Log($"Fatto: volume con {chapters.Count} capitoli.");
            return antiBot;
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// Numero e titolo del capitolo i-esimo: il numero indicato (+1 per ogni indirizzo), altrimenti quello
    /// della lista capitoli, altrimenti quello nel titolo della pagina, altrimenti la posizione.
    /// </summary>
    private ChapterInfo Chapter(Job job, int i, string cbzPath)
    {
        _found.TryGetValue(job.Urls[i], out var found);
        string number = job.FirstChapter is { } first ? Format(first + i)
            : found is not null ? Format(found.Number)
            : EpubMaker.NumberFromName(Path.GetFileNameWithoutExtension(cbzPath)) ?? (i + 1).ToString(CultureInfo.InvariantCulture);
        return new ChapterInfo { CbzPath = cbzPath, Number = number, Title = found?.Title ?? "" };
    }

    // "Dan Da Dan Capitolo 067 ITA" -> "Dan Da Dan"
    private static string SeriesFromTitle(string title)
    {
        var m = Regex.Match(title, @"^(.*?)\s*(?:[-–|:]\s*)?(?:capitolo|cap\.?|chapter|ch\.?|episodio|ep\.?)?\s*\d", RegexOptions.IgnoreCase);
        return m.Success && m.Groups[1].Value.Trim().Length > 0 ? m.Groups[1].Value.Trim() : title;
    }

    private static string Format(double n) => n.ToString(CultureInfo.InvariantCulture);

    // Vuoto = null; "12" o "12,5" = numero; altro = non valido
    private static bool TryParseOptional(string? text, out double? value)
    {
        value = null;
        string s = (text ?? "").Trim().Replace(',', '.');
        if (s.Length == 0) return true;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return false;
        value = n;
        return true;
    }

    /// <summary>Finestrella con una domanda e due pulsanti; true se si sceglie il primo.</summary>
    private async Task<bool> ConfirmAsync(string title, string message, string yes, string no)
    {
        bool result = false;
        var dialog = new Window
        {
            Title = title,
            Icon = Icon,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var btnYes = new Button { Content = yes, MinWidth = 120, Classes = { "accent" } };
        var btnNo = new Button { Content = no, MinWidth = 100 };
        btnYes.Click += (_, _) => { result = true; dialog.Close(); };
        btnNo.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { btnYes, btnNo } },
            },
        };
        await dialog.ShowDialog(this);
        return result;
    }

    private void SetRunning(bool running)
    {
        btnStart.IsEnabled = btnFind.IsEnabled = !running;
        btnCancel.IsEnabled = running;
        txtUrls.IsReadOnly = txtName.IsReadOnly = txtFolder.IsReadOnly = running;
        txtListUrl.IsReadOnly = txtFrom.IsReadOnly = txtTo.IsReadOnly = running;
        btnBrowse.IsEnabled = numMin.IsEnabled = chkShow.IsEnabled = chkReader.IsEnabled = btnKobo.IsEnabled = !running;
        chkEpub.IsEnabled = pnlEpub.IsEnabled = chkVolume.IsEnabled = !running;
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

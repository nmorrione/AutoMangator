using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using static Web2Comic.Loc;

namespace Web2Comic;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private string? _lastFile;
    private double _autoGrowth; // di quanto è stata allungata la finestra per far posto ai pannelli
    // Capitoli trovati con "Trova capitoli": numero e titolo noti per indirizzo
    private Dictionary<string, FoundChapter> _found = [];

    public MainWindow()
    {
        InitializeComponent();
        txtFolder.Text = Program.DefaultOutputFolder;
        // I testi impostati da codice vanno aggiornati a mano quando cambia la lingua
        Instance.LanguageChanged += OnLanguageChanged;
        OnLanguageChanged();
        // All'apertura la finestra prende l'altezza giusta per il contenuto (che varia con la lingua)
        Opened += (_, _) => FitHeight();
    }

    private void OnLanguageChanged()
    {
        UpdateStartLabel();
        foreach (var btn in new[] { btnLangIt, btnLangEn, btnLangEs })
            btn.Classes.Set("selected", (string?)btn.Tag == Instance.Language);
    }

    private void btnLanguage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code }) Instance.SetLanguage(code);
        // Testi più lunghi o più corti: la finestra si adatta
        FitHeight();
    }

    private async void btnBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(txtFolder.Text) ? await StorageProvider.TryGetFolderFromPathAsync(txtFolder.Text) : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = T("Main.FolderPicker"),
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

    private async void btnConvert_Click(object? sender, RoutedEventArgs e)
    {
        // Propone la cartella dell'ultimo .cbz creato, se c'è
        string? folder = _lastFile is not null ? Path.GetDirectoryName(_lastFile) : null;
        await new ConvertWindow(folder).ShowDialog(this);
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
        FitHeight();
    }

    private void chkParallel_IsCheckedChanged(object? sender, RoutedEventArgs e) =>
        numParallel.IsEnabled = chkParallel.IsChecked == true;

    private void chkVolume_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        pnlVolumeNumber.IsVisible = chkVolume.IsChecked == true;
        UpdateStartLabel();
        FitHeight();
    }

    /// <summary>
    /// Adatta l'altezza della finestra a quello che mostra: la allunga quando compaiono pannelli
    /// (senza superare lo schermo) e, quando spariscono, la riaccorcia di quanto l'aveva allungata.
    /// </summary>
    private void FitHeight()
    {
        // Si misura il contenuto (non la ScrollViewer che lo contiene) con altezza libera
        layout.Measure(new Size(Math.Max(0, formScroll.Bounds.Width), double.PositiveInfinity));
        // Le opzioni intere + quello che sta sotto (barra di avanzamento, separatore, log) + i margini
        double below = root.Bounds.Height - formScroll.Bounds.Height;
        double needed = layout.DesiredSize.Height + below + root.Margin.Top + root.Margin.Bottom;
        if (needed > Height)
        {
            double before = Height;
            Height = needed;
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
            _autoGrowth += Height - before;
        }
        else if (_autoGrowth > 0)
        {
            double shrink = Math.Min(_autoGrowth, Height - needed);
            Height -= shrink;
            _autoGrowth -= shrink;
        }
    }

    // "Crea CBZ", "Crea EPUB", "Crea volume CBZ", "Crea volume EPUB" (nella lingua scelta)
    private void UpdateStartLabel() =>
        btnStart.Content = T(chkVolume.IsChecked == true ? "Main.StartVolume" : "Main.Start", chkEpub.IsChecked == true ? "EPUB" : "CBZ");

    // ---------- Lista capitoli ----------

    private async void btnFind_Click(object? sender, RoutedEventArgs e)
    {
        string listUrl = (txtListUrl.Text ?? "").Trim();
        if (!Uri.TryCreate(listUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            Log(T("Log.ListMissing"));
            return;
        }
        if (!TryParseOptional(txtFrom.Text, out double? from) || !TryParseOptional(txtTo.Text, out double? to))
        {
            Log(T("Log.RangeInvalid", T("Main.From"), T("Main.To")));
            return;
        }

        SetRunning(true);
        _cts = new CancellationTokenSource();
        IProgress<string> log = new Progress<string>(Log);
        try
        {
            await using var maker = await CbzMaker.StartAsync(chkShow.IsChecked == true, log);
            var all = await maker.FindChaptersAsync(listUrl, log, _cts.Token);
            if (all.Count == 0)
            {
                log.Report(T("Log.ListNone"));
                return;
            }

            var chosen = all.Where(c => (from is null || c.Number >= from) && (to is null || c.Number <= to)).ToList();
            log.Report(T("Log.ListFound", all.Count, Format(all[0].Number), Format(all[^1].Number)));
            if (chosen.Count == 0)
            {
                log.Report(T("Log.ListNoneInRange", T("Main.From"), T("Main.To")));
                return;
            }

            _found = chosen.ToDictionary(c => c.Url);
            txtUrls.Text = string.Join(Environment.NewLine, chosen.Select(c => c.Url));
            log.Report(T("Log.ListInserted", chosen.Count, Format(chosen[0].Number), Format(chosen[^1].Number), btnStart.Content));
            log.Report("");
        }
        catch (SecurityCheckException ex)
        {
            log.Report($"⚠ {ex.Message}");
            chkShow.IsChecked = true;
            log.Report("⚠ " + T("Log.AntiBotRetry", T("Main.ShowBrowserShort"), T("Main.Find")));
        }
        catch (OperationCanceledException)
        {
            Log(T("Log.Cancelled"));
        }
        catch (Exception ex)
        {
            Log(T("Log.Error", ex.Message));
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
            Log(invalid is null ? T("Log.NoUrls") : T("Log.InvalidUrl", invalid));
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
            Parallel = chkParallel.IsChecked == true ? (int)(numParallel.Value ?? 3) : 1,
        };

        // Opzioni EPUB: dati della serie e numero del primo capitolo
        if (chkEpub.IsChecked == true)
        {
            job.Book = series.Read();
            if (job.Book.Series.Length == 0) { Log(T("Log.SeriesRequired")); return; }
            if (!TryParseOptional(txtChapter.Text, out job.FirstChapter))
            {
                Log(T("Log.ChapterInvalid", txtChapter.Text));
                return;
            }
        }

        SetRunning(true);
        _cts = new CancellationTokenSource();
        IProgress<string> log = new Progress<string>(Log);
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
                log.Report("⚠ " + T("Log.AntiBotRetry", T("Main.ShowBrowserShort"), btnStart.Content));
            }
        }
        catch (OperationCanceledException)
        {
            Log(T("Log.Cancelled"));
        }
        catch (Exception ex)
        {
            Log(T("Log.Error", ex.Message));
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
        /// <summary>Quanti capitoli scaricare insieme (1 = uno alla volta).</summary>
        public int Parallel { get; init; } = 1;
    }

    // Un file per capitolo (.cbz, oppure .epub / .kepub.epub). Restituisce true se un sito ha chiesto la verifica anti-bot.
    private async Task<bool> CreateChaptersAsync(CbzMaker maker, Job job, IProgress<string> log, CancellationToken ct)
    {
        var files = new string?[job.Urls.Count];
        var errors = await ForEachChapterAsync(job, log, ct, async (i, chapterLog) =>
        {
            // Con più indirizzi il nome scelto riceve un numero progressivo
            string? name = job.BaseName is null ? null : job.Urls.Count == 1 ? job.BaseName : $"{job.BaseName} {i + 1:D2}";
            string cbz = await maker.CreateAsync(job.Urls[i], job.Folder, name, job.MinSide, chapterLog, ct, job.ForceReader);
            if (job.Book is not { } book)
            {
                chapterLog.Report(T("Log.Created", cbz));
                files[i] = cbz;
                return;
            }
            var chapter = Chapter(job, i, cbz);
            string epub = await Task.Run(() => EpubMaker.Create(book, chapter, job.Folder));
            chapterLog.Report(T("Log.ChapterCreated", chapter.Number, epub));
            if (job.KeepCbz) chapterLog.Report(T("Log.Kept", cbz));
            else File.Delete(cbz);
            files[i] = epub;
        });
        _lastFile = files.LastOrDefault(f => f is not null) ?? _lastFile;
        log.Report(T("Log.Done", files.Count(f => f is not null), job.Urls.Count));
        return errors.Any(e => e is SecurityCheckException);
    }

    /// <summary>
    /// Esegue <paramref name="work"/> per ogni capitolo: uno alla volta, oppure in parallelo fino a
    /// <see cref="Job.Parallel"/> capitoli insieme. Restituisce l'errore di ogni capitolo (null se è andato bene).
    /// L'ordine dei risultati resta quello degli indirizzi, qualunque sia l'ordine in cui i capitoli finiscono.
    /// </summary>
    private async Task<Exception?[]> ForEachChapterAsync(Job job, IProgress<string> log, CancellationToken ct,
        Func<int, IProgress<string>, Task> work)
    {
        int n = job.Urls.Count;
        bool parallel = job.Parallel > 1 && n > 1;
        var errors = new Exception?[n];

        async Task Run(int i)
        {
            // In parallelo le righe dei capitoli si mescolano: ognuna porta il suo [numero/totale]
            IProgress<string> chapterLog = parallel ? new PrefixedLog(log, $"[{i + 1}/{n}] ") : log;
            if (!parallel && n > 1) log.Report(T("Log.ChapterOf", i + 1, n));
            try
            {
                await work(i, chapterLog);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors[i] = ex;
                chapterLog.Report(ex is SecurityCheckException ? $"⚠ {ex.Message}" : T("Log.Error", ex.Message));
            }
            if (!parallel) log.Report("");
        }

        if (!parallel)
        {
            for (int i = 0; i < n; i++) await Run(i);
            return errors;
        }

        log.Report(T("Log.Parallel", job.Parallel));
        // Il primo capitolo da solo: un'eventuale verifica anti-bot si fa una volta e la sessione,
        // condivisa, la ricorda per gli altri. Se il sito blocca il browser nascosto, inutile proseguire.
        await Run(0);
        if (errors[0] is SecurityCheckException blocked)
        {
            for (int i = 1; i < n; i++) errors[i] = blocked;
            return errors;
        }

        using var gate = new SemaphoreSlim(job.Parallel);
        await Task.WhenAll(Enumerable.Range(1, n - 1).Select(async i =>
        {
            await gate.WaitAsync(ct);
            try { await Run(i); }
            finally { gate.Release(); }
        }));
        log.Report("");
        return errors;
    }

    /// <summary>Aggiunge un prefisso a ogni riga del log (es. "[3/10] ").</summary>
    private sealed class PrefixedLog(IProgress<string> inner, string prefix) : IProgress<string>
    {
        public void Report(string value) =>
            inner.Report(string.Join("\n", value.Split('\n').Select(line => line.Length == 0 ? line : prefix + line)));
    }

    // Tutti i capitoli in un unico file. Ogni capitolo viene prima scaricato in una cartella temporanea.
    private async Task<bool> CreateVolumeAsync(CbzMaker maker, Job job, IProgress<string> log, CancellationToken ct)
    {
        string temp = Path.Combine(Path.GetTempPath(), "Web2Comic", Guid.NewGuid().ToString("N"));
        var chapters = new List<ChapterInfo>();
        var failed = new List<string>();
        bool antiBot = false;
        try
        {
            var cbzs = new string?[job.Urls.Count];
            var errors = await ForEachChapterAsync(job, log, ct, async (i, chapterLog) =>
                cbzs[i] = await maker.CreateAsync(job.Urls[i], temp, null, job.MinSide, chapterLog, ct, job.ForceReader));
            // Nell'ordine degli indirizzi, qualunque sia l'ordine in cui sono finiti
            for (int i = 0; i < job.Urls.Count; i++)
            {
                if (cbzs[i] is { } cbz)
                    chapters.Add(Chapter(job, i, cbz));
                else
                    failed.Add(_found.TryGetValue(job.Urls[i], out var f) ? T("Common.ChapterN", Format(f.Number)) : job.Urls[i]);
            }
            antiBot = errors.Any(e => e is SecurityCheckException);

            if (chapters.Count == 0)
            {
                log.Report(T("Log.VolumeNoChapters"));
                return antiBot;
            }
            if (failed.Count > 0)
            {
                string list = string.Join("\n", failed.Take(10).Select(f => "• " + f)) + (failed.Count > 10 ? "\n• " + T("Volume.AndOthers", failed.Count - 10) : "");
                string missing = failed.Count == 1 ? T("Volume.MissingOne", job.Urls.Count) : T("Volume.MissingMany", failed.Count, job.Urls.Count);
                string others = chapters.Count == 1 ? T("Volume.OthersOne") : T("Volume.OthersMany", chapters.Count);
                bool go = await ConfirmAsync(T("Volume.FailedTitle"), T("Volume.FailedQuestion", missing, list, others),
                    T("Volume.CreateAnyway"), T("Common.Cancel"));
                if (!go)
                {
                    log.Report(T("Log.VolumeNotCreated"));
                    return antiBot;
                }
                log.Report(T("Log.VolumeWithout", string.Join(", ", failed)));
            }

            log.Report(T("Log.Merging", chapters.Count));
            if (job.Book is { } book)
            {
                _lastFile = await Task.Run(() => EpubMaker.CreateVolume(book, chapters, job.VolumeNumber, job.Folder, job.BaseName));
                log.Report(T("Log.VolumeCreated", _lastFile));
                if (job.KeepCbz)
                {
                    // Stesso nome del volume EPUB, senza estensione (.epub o .kepub.epub)
                    string name = Path.GetFileName(_lastFile)[..^book.Extension.Length];
                    log.Report(T("Log.Kept", await Task.Run(() => CbzMaker.MergeCbz(chapters.Select(c => c.CbzPath), job.Folder, name))));
                }
            }
            else
            {
                string name = job.BaseName ?? T("Volume.CbzName", SeriesFromTitle(Path.GetFileNameWithoutExtension(chapters[0].CbzPath)),
                    EpubMaker.PadNumber(chapters[0].Number), EpubMaker.PadNumber(chapters[^1].Number));
                _lastFile = await Task.Run(() => CbzMaker.MergeCbz(chapters.Select(c => c.CbzPath), job.Folder, name));
                log.Report(T("Log.VolumeCreated", _lastFile));
            }
            log.Report(T("Log.VolumeDone", chapters.Count));
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
        var m = Regex.Match(title, @"^(.*?)\s*(?:[-–|:]\s*)?(?:capitolo|capítulo|capitulo|cap\.?|chapter|ch\.?|episodio|ep\.?)?\s*\d", RegexOptions.IgnoreCase);
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
        btnBrowse.IsEnabled = numMin.IsEnabled = chkShow.IsEnabled = chkReader.IsEnabled = btnConvert.IsEnabled = !running;
        chkEpub.IsEnabled = pnlEpub.IsEnabled = chkVolume.IsEnabled = chkParallel.IsEnabled = !running;
        numParallel.IsEnabled = !running && chkParallel.IsChecked == true;
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
        Instance.LanguageChanged -= OnLanguageChanged;
        base.OnClosing(e);
    }
}

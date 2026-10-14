using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.Playwright;
using static Web2Comic.Loc;

namespace Web2Comic;

/// <summary>
/// Apre pagine web con Playwright (Edge su Windows, WebKit altrove), raccoglie le immagini
/// nell'ordine in cui compaiono e le salva in un file .cbz (zip senza compressione).
/// Un'istanza tiene aperto il browser, così più pagine riusano lo stesso.
/// </summary>
public sealed partial class CbzMaker : IAsyncDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly bool _headless;

    private CbzMaker(IPlaywright playwright, IBrowser browser, bool headless)
    {
        _playwright = playwright;
        _browser = browser;
        _headless = headless;
    }

    public static async Task<CbzMaker> StartAsync(bool showBrowser, IProgress<string> log)
    {
        // Nell'app macOS il driver di Playwright sta in Contents/Resources/.playwright
        // (in Contents/MacOS la firma del bundle non accetta cartelle che non siano bundle)
        string resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
        if (OperatingSystem.IsMacOS() && Directory.Exists(Path.Combine(resources, ".playwright")))
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", resources);

        if (!OperatingSystem.IsWindows())
            await EnsureWebKitAsync(log);

        var playwright = await Playwright.CreateAsync();
        var browser = OperatingSystem.IsWindows()
            // Usa Microsoft Edge già installato: nessun browser da scaricare
            ? await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = !showBrowser })
            // WebKit (il motore di Safari) di Playwright, scaricato al primo avvio
            : await playwright.Webkit.LaunchAsync(new() { Headless = !showBrowser });
        return new CbzMaker(playwright, browser, !showBrowser);
    }

    // Scarica WebKit se manca; se è già presente l'installer termina subito
    private static async Task EnsureWebKitAsync(IProgress<string> log)
    {
        string cache = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches", "ms-playwright")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "ms-playwright");
        bool present = Directory.Exists(cache) && Directory.EnumerateDirectories(cache, "webkit-*").Any();
        if (!present)
            log.Report(T("Engine.WebKitDownload"));

        int exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "webkit"]));
        if (exitCode != 0)
            throw new InvalidOperationException(T("Engine.WebKitFailed", exitCode));
        if (!present)
            log.Report(T("Engine.WebKitReady"));
    }

    /// <summary>Crea il .cbz e restituisce il percorso del file creato.</summary>
    /// <param name="fileName">Nome del file senza estensione; null per usare il titolo della pagina.</param>
    /// <param name="forceReader">Sfoglia la pagina come un reader anche se non viene riconosciuto da solo.</param>
    public async Task<string> CreateAsync(string url, string outputFolder, string? fileName, int minSide,
        IProgress<string> log, CancellationToken ct, bool forceReader = false)
    {
        await using var context = await _browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 1000 } });
        var page = await context.NewPageAsync();

        // Annullare chiude la pagina: le chiamate Playwright in corso falliscono subito
        using var reg = ct.Register(() => _ = page.CloseAsync());
        try
        {
            return await CreateCoreAsync(page, context, url, outputFolder, fileName, minSide, forceReader, _headless, log, ct);
        }
        catch (PlaywrightException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }

    private static async Task<string> CreateCoreAsync(IPage page, IBrowserContext context, string url,
        string outputFolder, string? fileName, int minSide, bool forceReader, bool headless, IProgress<string> log, CancellationToken ct)
    {
        // Tiene da parte le immagini scaricate dal browser, così non vanno riscaricate
        var responses = new ConcurrentDictionary<string, IResponse>();
        page.Response += (_, r) => { if (r.Request.ResourceType == "image") responses[r.Url] = r; };
        var guard = await ProtectAsync(context, page, url);

        log.Report(T("Engine.Opening", url));
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
        await WaitForSecurityCheckAsync(page, headless, log, ct);
        guard.SiteLoaded(page.Url);

        log.Report(T("Engine.Scrolling"));
        await page.EvaluateAsync(ScrollToBottomJs);
        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 }); } catch (TimeoutException) { }
        ct.ThrowIfCancellationRequested();

        // Immagini in ordine di pagina, scartando quelle troppo piccole (loghi, icone, banner) e quelle
        // mostrate in piccolo anche se il file è grande (avatar dei commenti, miniature, promozioni)
        var srcs = await page.EvaluateAsync<string[]>(@"min => [...new Set(
            [...document.images]
                .filter(i => {
                    const r = i.getBoundingClientRect();
                    return i.complete && i.naturalWidth >= min && i.naturalHeight >= min && r.width >= 150 && r.height >= 150;
                })
                .map(i => i.currentSrc || i.src)
                .filter(s => s && !s.startsWith('blob:')))]", minSide);

        // Reader a una pagina per volta: le pagine si ottengono sfogliandolo
        if (forceReader || await LooksLikeReaderAsync(page))
        {
            if (await ReadReaderAsync(page, guard, forceReader, log, ct) is { } pages)
                srcs = pages;
        }

        if (srcs.Length == 0)
            throw new InvalidOperationException(T("Engine.NoImages"));
        // Una sola immagine orizzontale: di solito è l'avviso "capitolo non disponibile" del sito, non una pagina
        if (srcs.Length == 1 && await page.EvaluateAsync<bool>(@"src => {
                const i = [...document.images].find(x => (x.currentSrc || x.src) === src);
                return !!i && i.naturalWidth > i.naturalHeight;
            }", srcs[0]))
            throw new InvalidOperationException(T("Engine.NotAvailable"));
        log.Report(T("Engine.Found", srcs.Length));

        if (string.IsNullOrWhiteSpace(fileName))
        {
            string title = await page.TitleAsync();
            fileName = string.IsNullOrWhiteSpace(title) ? new Uri(url).Host : title;
        }
        Directory.CreateDirectory(outputFolder);
        string outPath = UniquePath(Path.Combine(outputFolder, SafeFileName(fileName) + ".cbz"));

        // Scrive su un file temporaneo: se qualcosa va storto non resta un .cbz a metà
        string tmpPath = outPath + ".part";
        try
        {
            using (var zip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
            {
                int n = 0;
                foreach (var src in srcs)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[]? data = await GetBytesAsync(src, responses, context, page.Url);
                    string? ext = data is null ? null : Extension(data);
                    if (data is null || ext is null) { log.Report("  " + T("Engine.Skipped", src)); continue; }

                    var entry = zip.CreateEntry($"{++n:D3}{ext}", CompressionLevel.NoCompression);
                    using var s = entry.Open();
                    s.Write(data);
                    log.Report($"  {n:D3}  {data.Length / 1024,6} KB");
                }
                if (n == 0) throw new InvalidOperationException(T("Engine.NoValidImages"));
            }
            File.Move(tmpPath, outPath);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }

        return outPath;
    }

    // Scorre fino in fondo (anche se la pagina si allunga caricando altro), per far partire il lazy-loading
    private const string ScrollToBottomJs = @"async () => {
        const sleep = ms => new Promise(r => setTimeout(r, ms));
        let stable = 0, lastHeight = 0;
        for (let i = 0; i < 500 && stable < 3; i++) {
            window.scrollBy(0, window.innerHeight * 0.8);
            await sleep(350);
            const h = document.documentElement.scrollHeight;
            const atBottom = window.scrollY + window.innerHeight >= h - 2;
            stable = (atBottom && h === lastHeight) ? stable + 1 : 0;
            lastHeight = h;
        }
    }";

    /// <summary>
    /// Unisce più .cbz in un unico volume, nell'ordine dato: le pagine vengono rinumerate
    /// (0001, 0002, ...) e restano non compresse, come nei capitoli.
    /// </summary>
    public static string MergeCbz(IEnumerable<string> cbzPaths, string outputFolder, string fileName)
    {
        Directory.CreateDirectory(outputFolder);
        string outPath = UniquePath(Path.Combine(outputFolder, SafeFileName(fileName) + ".cbz"));
        string tmpPath = outPath + ".part";
        try
        {
            using (var volume = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
            {
                int n = 0;
                foreach (var cbz in cbzPaths)
                {
                    using var chapter = ZipFile.OpenRead(cbz);
                    foreach (var page in chapter.Entries.Where(e => e.Length > 0).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
                    {
                        var entry = volume.CreateEntry($"{++n:D4}{Path.GetExtension(page.Name)}", CompressionLevel.NoCompression);
                        using var src = page.Open();
                        using var dst = entry.Open();
                        src.CopyTo(dst);
                    }
                }
            }
            File.Move(tmpPath, outPath);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
        return outPath;
    }

    private static async Task<byte[]?> GetBytesAsync(string src, ConcurrentDictionary<string, IResponse> responses,
        IBrowserContext context, string referer)
    {
        try
        {
            if (src.StartsWith("data:"))
                return Convert.FromBase64String(src[(src.IndexOf(',') + 1)..]);
            if (responses.TryGetValue(src, out var r))
                return await r.BodyAsync();
            var resp = await context.APIRequest.GetAsync(src, new() { Headers = new Dictionary<string, string> { ["Referer"] = referer } });
            return resp.Ok ? await resp.BodyAsync() : null;
        }
        catch (Exception ex) when (ex is PlaywrightException or FormatException)
        {
            return null;
        }
    }

    // Estensione dal contenuto reale del file (le URL spesso non ne hanno una affidabile);
    // null per formati non adatti a un fumetto (SVG, icone, ...)
    private static string? Extension(byte[] d) =>
        d is [0xFF, 0xD8, ..] ? ".jpg" :
        d is [0x89, 0x50, 0x4E, 0x47, ..] ? ".png" :
        d is [0x47, 0x49, 0x46, ..] ? ".gif" :
        d.Length > 12 && d[8..12] is [0x57, 0x45, 0x42, 0x50] ? ".webp" :
        d.Length > 12 && d[4..8] is [0x66, 0x74, 0x79, 0x70] ? ".avif" :
        null;

    internal static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Trim().TrimEnd('.');
    }

    // "nome.cbz" -> "nome (2).cbz" se esiste già
    private static string UniquePath(string path)
    {
        string dir = Path.GetDirectoryName(path)!, name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int i = 2; File.Exists(path); i++)
            path = Path.Combine(dir, $"{name} ({i}){ext}");
        return path;
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }
}

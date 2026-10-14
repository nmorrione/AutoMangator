using Avalonia;

namespace AutoMangator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Senza finestra: AutoMangator --cli <url> [cartella]  (usato anche dai test su GitHub Actions)
        // Opzioni in fondo: --reader sfoglia la pagina come un reader anche se non viene riconosciuto,
        // --show mostra il browser (per i siti con verifica di sicurezza)
        if (args is ["--cli", var url, ..])
        {
            string[] flags = ["--reader", "--show"];
            var rest = args.Skip(2).Where(a => !flags.Contains(a)).ToArray();
            return RunCliAsync(url, rest.Length > 0 ? rest[0] : DefaultOutputFolder,
                args.Contains("--reader"), args.Contains("--show")).GetAwaiter().GetResult();
        }
        // AutoMangator --epub <cartella con i .cbz> <serie> [autore]
        if (args is ["--epub", var folder, var series, ..])
            return RunEpubCli(folder, new BookInfo { Series = series, Writer = args.Length > 3 ? args[3] : "" });

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private static async Task<int> RunCliAsync(string url, string folder, bool forceReader, bool showBrowser)
    {
        var log = new ConsoleLog();
        try
        {
            await using var maker = await CbzMaker.StartAsync(showBrowser, log);
            string file = await maker.CreateAsync(url, folder, null, 300, log, CancellationToken.None, forceReader);
            Console.WriteLine($"Creato {file}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERRORE: {ex}");
            return 1;
        }
    }

    private static int RunEpubCli(string folder, BookInfo book)
    {
        var chapters = EpubMaker.LoadChapters(folder);
        if (chapters.Count == 0)
        {
            Console.Error.WriteLine("ERRORE: nessun file .cbz nella cartella.");
            return 1;
        }
        try
        {
            foreach (var ch in chapters)
                Console.WriteLine($"Creato {EpubMaker.Create(book, ch, Path.Combine(folder, EpubMaker.OutputFolderName))}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERRORE: {ex}");
            return 1;
        }
    }

    // Scrive subito e in ordine (Progress<T> passerebbe dal thread pool)
    private sealed class ConsoleLog : IProgress<string>
    {
        public void Report(string value) => Console.WriteLine(value);
    }
}

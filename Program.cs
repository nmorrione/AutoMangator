using Avalonia;

namespace AutoMangator;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Senza finestra: AutoMangator --cli <url> [cartella]  (usato anche dai test su GitHub Actions)
        if (args is ["--cli", var url, ..])
            return RunCliAsync(url, args.Length > 2 ? args[2] : DefaultOutputFolder).GetAwaiter().GetResult();
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

    private static async Task<int> RunCliAsync(string url, string folder)
    {
        var log = new ConsoleLog();
        try
        {
            await using var maker = await CbzMaker.StartAsync(showBrowser: false, log);
            string file = await maker.CreateAsync(url, folder, null, 300, log, CancellationToken.None);
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

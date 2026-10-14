using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Web2Comic.Loc;

namespace Web2Comic;

/// <summary>Un capitolo trovato nella pagina con la lista dei capitoli.</summary>
public sealed record FoundChapter(string Url, double Number, string Title);

// Lista dei capitoli: dalla pagina che elenca i capitoli di una serie si ricavano gli indirizzi
// dei singoli capitoli, riconoscibili perché hanno tutti lo stesso schema e cambia solo il numero
// (es. .../reader/001, .../reader/002, ...).
public sealed partial class CbzMaker
{
    /// <summary>Apre la pagina della lista e restituisce i capitoli trovati, in ordine di numero.</summary>
    public async Task<List<FoundChapter>> FindChaptersAsync(string listUrl, IProgress<string> log, CancellationToken ct)
    {
        // Stessa sessione dei capitoli: se la lista chiede una verifica anti-bot, vale anche per loro
        var page = await (await ContextAsync()).NewPageAsync();
        using var reg = ct.Register(() => _ = page.CloseAsync());
        try
        {
            var guard = await ProtectAsync(page, listUrl);
            log.Report(T("Engine.OpeningList", listUrl));
            await page.GotoAsync(listUrl, new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
            await WaitForSecurityCheckAsync(page, _headless, log, ct);
            guard.SiteLoaded(page.Url);

            // Alcune liste caricano altri capitoli scorrendo
            await page.EvaluateAsync(ScrollToBottomJs);
            ct.ThrowIfCancellationRequested();

            var links = await page.EvaluateAsync<PageLink[]>(@"() => [...document.querySelectorAll('a[href]')]
                .map(a => ({ href: a.href, text: a.textContent.trim().replace(/\s+/g, ' ') }))");
            return PickChapters(page.Url, links);
        }
        catch (PlaywrightException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        finally
        {
            try { await page.CloseAsync(); } catch (PlaywrightException) { }
        }
    }

    private sealed class PageLink
    {
        public string Href { get; set; } = "";
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// Raggruppa i link per "schema" (l'indirizzo con l'ultimo numero sostituito da {n}) e tiene il gruppo
    /// più numeroso, preferendo quelli dentro il percorso della lista: così restano fuori menu, pubblicità
    /// e i link ai capitoli di altre serie (es. nei commenti).
    /// </summary>
    private static List<FoundChapter> PickChapters(string listUrl, IEnumerable<PageLink> links)
    {
        var list = new Uri(listUrl);
        // ".../dan-da-dan/lista-capitoli" -> ".../dan-da-dan/"
        string seriesPath = list.AbsolutePath[..(list.AbsolutePath.TrimEnd('/').LastIndexOf('/') + 1)];

        var groups = new Dictionary<string, Dictionary<string, (double Number, string Title)>>();
        foreach (var link in links)
        {
            if (!Uri.TryCreate(link.Href, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https")) continue;
            if (!u.Host.Equals(list.Host, StringComparison.OrdinalIgnoreCase)) continue;
            string url = u.GetLeftPart(UriPartial.Query); // senza #ancora
            if (url.TrimEnd('/') == list.GetLeftPart(UriPartial.Query).TrimEnd('/')) continue;

            var m = LastNumberInUrl().Match(u.PathAndQuery);
            if (!m.Success) continue;
            string template = u.Host + u.PathAndQuery[..m.Index] + "{n}" + u.PathAndQuery[(m.Index + m.Length)..];
            double number = double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture);

            if (!groups.TryGetValue(template, out var g)) groups[template] = g = [];
            // Lo stesso capitolo può comparire più volte (copertina + titolo): si tiene il testo più lungo
            if (!g.TryGetValue(url, out var prev) || link.Text.Length > prev.Title.Length)
                g[url] = (number, link.Text);
        }

        var best = groups
            .Where(g => g.Value.Count >= 2)
            .OrderByDescending(g => g.Key.Contains(seriesPath, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(g => g.Value.Count)
            .Select(g => g.Value)
            .FirstOrDefault();
        if (best is null) return [];

        return best.Select(kv => new FoundChapter(kv.Key, kv.Value.Number, CleanTitle(kv.Value.Title)))
            .OrderBy(c => c.Number)
            .ToList();
    }

    // "Capitolo 067 - Ma quanto è forte?" -> "Ma quanto è forte?"; "Chapter 12" -> ""
    internal static string CleanTitle(string text) =>
        ChapterPrefix().Replace(text, "").Trim();

    // L'ultimo numero nel percorso (o nella query) dell'indirizzo, con eventuale parte decimale
    [GeneratedRegex(@"\d+(?:[.,]\d+)?(?!.*\d)")]
    private static partial Regex LastNumberInUrl();

    // Italiano, inglese e spagnolo: "Capitolo", "Chapter", "Capítulo", "Episodio", "Episode"...
    [GeneratedRegex(@"^\s*(capitolo|capítulo|capitulo|cap\.?|chapter|ch\.?|episodio|episode|ep\.?)?\s*[\d.,]+\s*[-:–—]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterPrefix();
}

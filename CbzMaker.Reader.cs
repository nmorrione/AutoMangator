using Microsoft.Playwright;
using static Web2Comic.Loc;

namespace Web2Comic;

/// <summary>Il sito ha mostrato una verifica anti-bot al browser nascosto: va ripetuto col browser visibile.</summary>
public sealed class SecurityCheckException(string url) : InvalidOperationException(
    T("Engine.AntiBot", new Uri(url).Host, T("Main.ShowBrowserShort")));

// Reader a una pagina per volta: invece di mostrare tutto il capitolo, alcuni siti mostrano
// un'immagine alla volta. Qui si "sfoglia" il reader come farebbe una persona, raccogliendo
// le pagine, e si impedisce a pubblicità e link di portare il browser fuori dal capitolo.
public sealed partial class CbzMaker
{
    /// <summary>
    /// Decide quali navigazioni della pagina principale sono permesse: prima del caricamento tutto
    /// (redirect iniziali), poi solo lo stesso sito e, mentre si sfoglia un reader, solo il capitolo.
    /// </summary>
    private sealed class NavigationGuard(string startUrl)
    {
        private string _host = new Uri(startUrl).Host;
        private bool _active;
        private string? _chapterPath;

        public int Blocked { get; private set; }

        /// <summary>Pagina caricata: da qui in poi si resta su questo sito.</summary>
        public void SiteLoaded(string url)
        {
            _host = new Uri(url).Host;
            _active = true;
        }

        /// <summary>Si sta sfogliando un reader: si resta dentro l'indirizzo del capitolo.</summary>
        public void LockChapter(string url) => _chapterPath = new Uri(url).AbsolutePath.TrimEnd('/');

        public bool Allows(string url)
        {
            bool ok = Check(url);
            if (!ok) Blocked++;
            return ok;
        }

        private bool Check(string url)
        {
            if (!_active) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "http" && u.Scheme != "https"))
                return false;
            if (!SameSite(u.Host)) return false;
            if (_chapterPath is null) return true;
            // Stesso capitolo: stesso percorso (cambia solo ?pagina=...) o un sotto-percorso (/capitolo/2)
            string path = u.AbsolutePath.TrimEnd('/');
            return path.Equals(_chapterPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(_chapterPath + "/", StringComparison.OrdinalIgnoreCase);
        }

        // "sito.com", "www.sito.com" e "img.sito.com" contano come lo stesso sito
        private bool SameSite(string host) =>
            host.Equals(_host, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + _host, StringComparison.OrdinalIgnoreCase)
            || _host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Niente popup, nuove schede o navigazioni verso altri siti (pubblicità).</summary>
    private static async Task<NavigationGuard> ProtectAsync(IPage page, string url)
    {
        var guard = new NavigationGuard(url);

        // window.open è già disattivato nella sessione (vedi ContextAsync); le schede che la pagina
        // aprisse comunque vengono chiuse. Solo quelle aperte da lei: le altre schede sono gli altri
        // capitoli scaricati in parallelo.
        page.Popup += (_, popup) => _ = popup.CloseAsync();

        await page.RouteAsync("**/*", async route =>
        {
            var req = route.Request;
            if (req.IsNavigationRequest && req.Frame == page.MainFrame && !guard.Allows(req.Url))
                await route.AbortAsync();
            else
                await route.ContinueAsync();
        });
        return guard;
    }

    // Pagine di verifica anti-bot (Cloudflare e simili) mostrate al posto del sito
    private const string SecurityCheckJs = @"() => {
        const text = (document.title + ' ' + (document.body ? document.body.innerText.slice(0, 2000) : '')).toLowerCase();
        return /just a moment|ci siamo quasi|verifica di sicurezza|checking your browser|verify you are human|attention required|un momento…|verificación de seguridad|verifica que eres un ser humano/.test(text)
            || !!document.querySelector('#challenge-form, #challenge-running, iframe[src*=""challenges.cloudflare.com""]');
    }";

    /// <summary>
    /// Se il sito mostra una verifica anti-bot: col browser nascosto spiega come procedere,
    /// col browser visibile aspetta che la persona la completi nella finestra.
    /// </summary>
    private static async Task WaitForSecurityCheckAsync(IPage page, bool headless, IProgress<string> log, CancellationToken ct)
    {
        if (!await IsSecurityCheckAsync(page)) return;
        if (headless)
            throw new SecurityCheckException(page.Url);

        log.Report(T("Engine.CheckWaiting"));
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (await IsSecurityCheckAsync(page))
        {
            if (DateTime.UtcNow > deadline)
                throw new InvalidOperationException(T("Engine.CheckTimeout"));
            await Task.Delay(1000, ct);
        }
        try { await page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 60_000 }); } catch (TimeoutException) { }
        log.Report(T("Engine.CheckPassed"));
    }

    private static async Task<bool> IsSecurityCheckAsync(IPage page)
    {
        try { return await page.EvaluateAsync<bool>(SecurityCheckJs); }
        catch (PlaywrightException) { return true; } // la pagina sta cambiando (verifica in corso)
    }

    // Poche immagini grandi a schermo (di solito una): probabilmente è un reader
    private static async Task<bool> LooksLikeReaderAsync(IPage page)
    {
        int big = await page.EvaluateAsync<int>(@"() => [...document.images].filter(i => {
            const r = i.getBoundingClientRect();
            return i.naturalWidth > 0 && r.width >= 250 && r.height >= 350;
        }).length");
        return big is >= 1 and <= 2;
    }

    // L'immagine più grande a schermo è la pagina del reader. Va cercata ogni volta: molti reader
    // sostituiscono l'elemento <img> a ogni pagina. Mentre la nuova pagina carica, la più grande
    // potrebbe essere un'altra grafica del sito: per questo si accettano solo immagini caricate
    // e grandi almeno quanto una parte (minArea) della prima pagina.
    private const string MainImageJs = @"minArea => {
        let best = null, area = 0;
        for (const i of document.images) {
            const r = i.getBoundingClientRect();
            const a = r.width * r.height;
            if (i.complete && i.naturalWidth > 0 && a >= minArea && a > area) { best = i; area = a; }
        }
        return best ? { src: best.currentSrc || best.src, area } : null;
    }";

    private sealed class MainImage
    {
        public string Src { get; set; } = "";
        public double Area { get; set; }
    }

    // Pulsanti "pagina successiva", esclusi quelli per cambiare capitolo o volume
    private const string NextButtonJs = @"() => {
        const bad = /cap|chapter|episod|volum|comment|login|regist/i;
        const good = /^(next|avanti|successiv|seguente|›|»|>|→|▶|⟩)/i;
        for (const el of document.querySelectorAll('a, button, [role=button]')) {
            const r = el.getBoundingClientRect();
            if (r.width === 0 || r.height === 0) continue;
            const text = (el.textContent || el.getAttribute('aria-label') || el.title || '').trim();
            const meta = [text, el.getAttribute('aria-label'), el.title, el.id, String(el.className)].join(' ');
            if (bad.test(meta)) continue;
            if (good.test(text) || /(^|[-_ ])next([-_ ]|page|$)/i.test(el.id + ' ' + String(el.className))) {
                el.click();
                return true;
            }
        }
        return false;
    }";

    private enum TurnMethod { ArrowKey, ClickRight, NextButton }

    /// <summary>
    /// Sfoglia il reader e restituisce le pagine in ordine. Null se la pagina non si comporta
    /// da reader (e non è stato forzato): allora valgono le immagini trovate normalmente.
    /// </summary>
    private static async Task<string[]?> ReadReaderAsync(IPage page, NavigationGuard guard, bool forced,
        IProgress<string> log, CancellationToken ct)
    {
        await page.EvaluateAsync("() => window.scrollTo(0, 0)");
        var main = await page.EvaluateAsync<MainImage?>(MainImageJs, 0);
        if (main is null) return null;
        string first = main.Src;
        // Le pagine successive devono essere grandi almeno il 40% della prima
        double minArea = main.Area * 0.4;

        guard.LockChapter(page.Url);
        log.Report(T(forced ? "Engine.ReaderForced" : "Engine.ReaderProbe"));

        var pages = new List<string> { first };
        // Il clic sull'immagine solo se forzato: su pagine normali può aprire visualizzatori o gallerie
        // e farle scambiare per un reader
        TurnMethod[] all = forced
            ? [TurnMethod.ArrowKey, TurnMethod.ClickRight, TurnMethod.NextButton]
            : [TurnMethod.ArrowKey, TurnMethod.NextButton];
        TurnMethod? working = null;
        while (pages.Count < 1000)
        {
            ct.ThrowIfCancellationRequested();
            string? next = null;
            // Prima il metodo che ha già funzionato, poi gli altri
            foreach (var method in working is { } w ? [w, .. all.Where(m => m != w)] : all)
            {
                await TurnPageAsync(page, method);
                next = await WaitForNewImageAsync(page, pages[^1], minArea, ct);
                if (next is not null) { working = method; break; }
            }
            // Fine: l'immagine non cambia più, oppure si ripresenta una pagina già vista
            if (next is null)
            {
                if (pages.Count > 1) log.Report(T("Engine.ReaderEnd"));
                break;
            }
            if (pages.Contains(next))
            {
                log.Report(T("Engine.ReaderLoop", ShortName(next)));
                break;
            }

            pages.Add(next);
            if (pages.Count == 2) log.Report(T("Engine.ReaderDetected") + "\n  " + T("Engine.ReaderPage", 1, ShortName(pages[0])));
            log.Report("  " + T("Engine.ReaderPage", pages.Count, ShortName(next)));
        }

        if (guard.Blocked > 0)
            log.Report(T("Engine.Blocked", guard.Blocked));
        if (pages.Count == 1)
        {
            if (!forced) return null;
            log.Report(T("Engine.ReaderStuck"));
        }
        return [.. pages];
    }

    // "https://sito/manga/cap67/05.jpg?x=1" -> "05.jpg"
    private static string ShortName(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) ? Path.GetFileName(u.AbsolutePath) : url;

    private static async Task TurnPageAsync(IPage page, TurnMethod method)
    {
        try
        {
            switch (method)
            {
                case TurnMethod.ArrowKey:
                    await page.Keyboard.PressAsync("ArrowRight");
                    break;
                case TurnMethod.ClickRight:
                    // Clic sul lato destro della pagina, dopo averla portata al centro dello schermo
                    var box = await page.EvaluateAsync<double[]?>(@"() => {
                        let best = null, area = 0;
                        for (const i of document.images) {
                            const r = i.getBoundingClientRect();
                            if (i.naturalWidth > 0 && r.width * r.height > area) { best = i; area = r.width * r.height; }
                        }
                        if (!best) return null;
                        best.scrollIntoView({ block: 'center' });
                        const r = best.getBoundingClientRect();
                        return [r.left, r.top, r.width, r.height];
                    }");
                    if (box is [var x, var y, var w, var h])
                        await page.Mouse.ClickAsync((float)(x + w * 0.85), (float)(y + h / 2));
                    break;
                case TurnMethod.NextButton:
                    await page.EvaluateAsync<bool>(NextButtonJs);
                    break;
            }
        }
        catch (PlaywrightException) { } // la pagina sta cambiando: se ne accorge l'attesa successiva
    }

    // Aspetta che l'immagine principale diventi un'altra; null se non cambia
    private static async Task<string?> WaitForNewImageAsync(IPage page, string current, double minArea, CancellationToken ct)
    {
        string startUrl = page.Url;
        var deadline = DateTime.UtcNow.AddMilliseconds(2500);
        bool extended = false;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(150, ct);
            // Reader con un indirizzo per ogni pagina: lascia il tempo di caricarla
            if (!extended && page.Url != startUrl)
            {
                deadline = DateTime.UtcNow.AddSeconds(15);
                extended = true;
            }
            try
            {
                var img = await page.EvaluateAsync<MainImage?>(MainImageJs, minArea);
                if (img is not null && img.Src != current) return img.Src;
            }
            catch (PlaywrightException) { } // contesto distrutto da una navigazione: riprova
        }
        return null;
    }
}

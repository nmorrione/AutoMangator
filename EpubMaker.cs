using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace Web2Comic;

/// <summary>Dati comuni a tutti i capitoli di una serie.</summary>
public sealed class BookInfo
{
    public string Series { get; set; } = "";
    public string Writer { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Publisher { get; set; } = "";
    public string Year { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Language { get; set; } = "it";
    public string Summary { get; set; } = "";
    public bool RightToLeft { get; set; }
    /// <summary>
    /// Estensione .kepub.epub invece di .epub: il contenuto è lo stesso, ma così il Kobo apre il file
    /// col suo lettore, che gestisce bene le pagine a layout fisso dei fumetti.
    /// </summary>
    public bool Kobo { get; set; }

    public string Extension => Kobo ? ".kepub.epub" : ".epub";
}

/// <summary>Un capitolo: il .cbz di partenza con numero e titolo modificabili.</summary>
public sealed class ChapterInfo
{
    public required string CbzPath { get; init; }
    public string FileName => Path.GetFileName(CbzPath);
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
}

/// <summary>
/// Trasforma un .cbz in un EPUB 3 standard a layout fisso, con titolo, autore, serie e numero del
/// capitolo nei metadati (.kepub.epub se ottimizzato per Kobo). Le immagini restano quelle originali;
/// solo WebP e AVIF, che molti ebook reader non mostrano, vengono convertiti in JPEG.
/// </summary>
public static partial class EpubMaker
{
    /// <summary>Sottocartella dove "Converti CBZ in EPUB" mette gli EPUB.</summary>
    public const string OutputFolderName = "EPUB";
    // Nome usato fino alla 1.5: le cartelle già create continuano a essere lette
    private const string LegacyOutputFolderName = "Kobo";

    /// <summary>Elenca i .cbz della cartella, con il numero preso dal nome del file.</summary>
    public static List<ChapterInfo> LoadChapters(string folder)
    {
        var chapters = Directory.GetFiles(folder, "*.cbz")
            .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
            .Select(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                return new ChapterInfo { CbzPath = f, Number = NumberFromName(name) ?? "", Title = name };
            }).ToList();

        // I file senza numero nel nome vanno in coda, dopo il numero più alto
        double max = chapters.Where(c => c.Number != "").Select(c => ParseNumber(c.Number)).DefaultIfEmpty(0).Max();
        int next = (int)Math.Floor(max) + 1;
        foreach (var c in chapters.Where(c => c.Number == ""))
            c.Number = (next++).ToString(CultureInfo.InvariantCulture);

        return chapters.OrderBy(c => ParseNumber(c.Number)).ThenBy(c => c.FileName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>L'ultimo numero nel nome ("Capitolo 12" -> "12", "Cap. 12,5" -> "12.5"), o null se non ce ne sono.</summary>
    public static string? NumberFromName(string name)
    {
        var m = LastNumber().Match(name);
        return m.Success ? NormalizeNumber(m.Value) : null;
    }

    /// <summary>Rilegge i dati comuni da un EPUB già creato in precedenza, se c'è.</summary>
    public static BookInfo? TryReadExisting(string folder)
    {
        // Nella sottocartella EPUB (finestra "Converti CBZ in EPUB"), in quella Kobo delle versioni
        // precedenti o nella cartella stessa (finestra principale)
        string? epub = new[] { Path.Combine(folder, OutputFolderName), Path.Combine(folder, LegacyOutputFolderName), folder }
            .Where(Directory.Exists)
            .Select(d => Directory.GetFiles(d, "*.epub").Order().FirstOrDefault())
            .FirstOrDefault(f => f is not null);
        if (epub is null) return null;
        try
        {
            using var zip = ZipFile.OpenRead(epub);
            var entry = zip.GetEntry("OEBPS/content.opf");
            if (entry is null) return null;
            using var s = entry.Open();
            var opf = XDocument.Load(s);
            XNamespace dc = "http://purl.org/dc/elements/1.1/", ns = "http://www.idpf.org/2007/opf";
            var meta = opf.Root!.Element(ns + "metadata")!;
            string Role(string role) =>
                meta.Elements(dc + "creator").FirstOrDefault(c => meta.Elements(ns + "meta")
                    .Any(m => (string?)m.Attribute("refines") == "#" + (string?)c.Attribute("id") && m.Value == role))?.Value ?? "";
            return new BookInfo
            {
                Series = (string?)meta.Elements(ns + "meta").FirstOrDefault(m => (string?)m.Attribute("name") == "calibre:series")?.Attribute("content") ?? "",
                Writer = Role("aut"),
                Artist = Role("art"),
                Publisher = meta.Element(dc + "publisher")?.Value ?? "",
                Year = meta.Element(dc + "date")?.Value ?? "",
                Genre = string.Join(", ", meta.Elements(dc + "subject").Select(e => e.Value)),
                Language = meta.Element(dc + "language")?.Value ?? "it",
                Summary = meta.Element(dc + "description")?.Value ?? "",
                RightToLeft = (string?)opf.Root.Element(ns + "spine")?.Attribute("page-progression-direction") == "rtl",
                Kobo = epub.EndsWith(".kepub.epub", StringComparison.OrdinalIgnoreCase),
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { return null; }
    }

    /// <summary>Titolo mostrato dall'ebook reader: "Serie 012 - Titolo", così i capitoli restano in ordine.</summary>
    public static string BookTitle(BookInfo book, ChapterInfo ch)
    {
        string title = $"{book.Series} {PadNumber(ch.Number)}";
        return string.IsNullOrWhiteSpace(ch.Title) ? title : $"{title} - {ch.Title.Trim()}";
    }

    /// <summary>Crea l'EPUB di un capitolo e restituisce il percorso del file.</summary>
    public static string Create(BookInfo book, ChapterInfo ch, string outputFolder)
    {
        string title = BookTitle(book, ch);
        string path = Path.Combine(outputFolder, CbzMaker.SafeFileName($"{book.Series} {PadNumber(ch.Number)}") + book.Extension);
        // Stesso identificativo per lo stesso capitolo: ricreare l'EPUB non lo duplica
        return Write(book, [ch], title, NormalizeNumber(ch.Number), Identifier($"{book.Series.Trim().ToLowerInvariant()}|{NormalizeNumber(ch.Number)}"),
            path, chapterToc: false);
    }

    /// <summary>
    /// Crea un unico EPUB con tutti i capitoli, nell'ordine dato, e un indice con un punto per capitolo.
    /// Titolo "Serie Vol. 02" se c'è il numero del volume, altrimenti "Serie 001-010".
    /// </summary>
    public static string CreateVolume(BookInfo book, IReadOnlyList<ChapterInfo> chapters, string? volume, string outputFolder, string? fileName)
    {
        string range = $"{PadNumber(chapters[0].Number)}-{PadNumber(chapters[^1].Number)}";
        bool hasVolume = !string.IsNullOrWhiteSpace(volume);
        string title = hasVolume ? $"{book.Series} Vol. {PadVolume(volume!)}" : $"{book.Series} {range}";
        string index = NormalizeNumber(hasVolume ? volume! : chapters[0].Number);
        string name = string.IsNullOrWhiteSpace(fileName) ? title : fileName.Trim();
        string path = Path.Combine(outputFolder, CbzMaker.SafeFileName(name) + book.Extension);
        return Write(book, chapters, title, index, Identifier($"{book.Series.Trim().ToLowerInvariant()}|volume|{title.ToLowerInvariant()}"),
            path, chapterToc: true);
    }

    /// <summary>
    /// Voce dell'indice di un capitolo: "Capitolo 012 - Titolo", nella lingua del libro
    /// (non dell'interfaccia: l'indice fa parte del contenuto).
    /// </summary>
    public static string ChapterLabel(ChapterInfo ch, string language)
    {
        string label = Loc.In(language.Trim().ToLowerInvariant(), "Common.ChapterN", PadNumber(ch.Number));
        return string.IsNullOrWhiteSpace(ch.Title) ? label : $"{label} - {ch.Title.Trim()}";
    }

    private sealed record Page(string Id, string Extension, string MediaType, int Width, int Height);

    private static string Write(BookInfo book, IReadOnlyList<ChapterInfo> chapters, string title, string index, string id,
        string outPath, bool chapterToc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        string tmpPath = outPath + ".part";
        var pages = new List<Page>();
        var toc = new List<(string Label, string PageId)>();
        try
        {
            using (var zip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
            {
                // "mimetype" deve essere il primo file e non compresso
                Add(zip, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
                Add(zip, "META-INF/container.xml", ContainerXml);
                foreach (var ch in chapters)
                {
                    int before = pages.Count;
                    // Le immagini vengono scritte subito: anche un volume molto grande non sta tutto in memoria
                    WritePages(zip, ch.CbzPath, pages);
                    if (pages.Count == before)
                        throw new InvalidOperationException(Loc.T("Epub.NoImages", ch.FileName));
                    toc.Add((chapterToc ? ChapterLabel(ch, book.Language) : title, pages[before].Id));
                }
                Add(zip, "OEBPS/content.opf", Opf(book, title, index, id, pages));
                Add(zip, "OEBPS/nav.xhtml", Nav(title, toc));
                Add(zip, "OEBPS/toc.ncx", Ncx(title, id, toc));
            }
            File.Move(tmpPath, outPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
        return outPath;
    }

    // Aggiunge all'EPUB le pagine di un .cbz (immagine + pagina XHTML), numerandole dopo quelle già presenti
    private static void WritePages(ZipArchive epub, string cbzPath, List<Page> pages)
    {
        using var cbz = ZipFile.OpenRead(cbzPath);
        foreach (var entry in cbz.Entries.Where(e => e.Length > 0).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            byte[] data = ms.ToArray();

            using var codec = SKCodec.Create(new MemoryStream(data));
            if (codec is null) continue; // non è un'immagine (es. ComicInfo.xml)

            string id = $"p{pages.Count + 1:D4}";
            var (w, h) = (codec.Info.Width, codec.Info.Height);
            Page page;
            switch (codec.EncodedFormat)
            {
                case SKEncodedImageFormat.Jpeg: page = new(id, ".jpg", "image/jpeg", w, h); break;
                case SKEncodedImageFormat.Png: page = new(id, ".png", "image/png", w, h); break;
                case SKEncodedImageFormat.Gif: page = new(id, ".gif", "image/gif", w, h); break;
                default:
                    // WebP, AVIF, ...: molti ebook reader (Kobo compreso) non li mostrano negli EPUB
                    using (var bmp = SKBitmap.Decode(data))
                    using (var img = SKImage.FromBitmap(bmp))
                    using (var jpg = img.Encode(SKEncodedImageFormat.Jpeg, 92))
                    {
                        data = jpg.ToArray();
                        page = new(id, ".jpg", "image/jpeg", bmp.Width, bmp.Height);
                    }
                    break;
            }

            Add(epub, $"OEBPS/Text/{page.Id}.xhtml", PageXhtml(page));
            using (var s = epub.CreateEntry($"OEBPS/Images/{page.Id}{page.Extension}", CompressionLevel.NoCompression).Open())
                s.Write(data);
            pages.Add(page);
        }
    }

    private static string Opf(BookInfo book, string title, string index, string id, List<Page> pages)
    {
        XNamespace ns = "http://www.idpf.org/2007/opf", dc = "http://purl.org/dc/elements/1.1/";
        var meta = new XElement(ns + "metadata", new XAttribute(XNamespace.Xmlns + "dc", dc),
            new XElement(dc + "identifier", new XAttribute("id", "bookid"), id),
            new XElement(dc + "title", title),
            new XElement(dc + "language", string.IsNullOrWhiteSpace(book.Language) ? "it" : book.Language.Trim()),
            new XElement(ns + "meta", new XAttribute("property", "dcterms:modified"), DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
            new XElement(ns + "meta", new XAttribute("property", "rendition:layout"), "pre-paginated"),
            new XElement(ns + "meta", new XAttribute("property", "rendition:spread"), "none"),
            new XElement(ns + "meta", new XAttribute("property", "rendition:orientation"), "auto"),
            new XElement(ns + "meta", new XAttribute("name", "cover"), new XAttribute("content", "cover")),
            // Serie e numero: formato calibre (letto da Kobo con NickelSeries) e formato EPUB 3
            new XElement(ns + "meta", new XAttribute("name", "calibre:series"), new XAttribute("content", book.Series)),
            new XElement(ns + "meta", new XAttribute("name", "calibre:series_index"), new XAttribute("content", index)),
            new XElement(ns + "meta", new XAttribute("property", "belongs-to-collection"), new XAttribute("id", "series"), book.Series),
            new XElement(ns + "meta", new XAttribute("refines", "#series"), new XAttribute("property", "collection-type"), "series"),
            new XElement(ns + "meta", new XAttribute("refines", "#series"), new XAttribute("property", "group-position"), index));

        // Il primo autore è quello che gli ebook reader mostrano in libreria
        void Creator(string id, string name, string role)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            meta.Add(new XElement(dc + "creator", new XAttribute("id", id), name.Trim()),
                new XElement(ns + "meta", new XAttribute("refines", "#" + id), new XAttribute("property", "role"), new XAttribute("scheme", "marc:relators"), role));
        }
        Creator("aut", book.Writer, "aut");
        if (!string.Equals(book.Artist.Trim(), book.Writer.Trim(), StringComparison.OrdinalIgnoreCase))
            Creator("art", book.Artist, "art");

        if (!string.IsNullOrWhiteSpace(book.Publisher)) meta.Add(new XElement(dc + "publisher", book.Publisher.Trim()));
        if (!string.IsNullOrWhiteSpace(book.Year)) meta.Add(new XElement(dc + "date", book.Year.Trim()));
        if (!string.IsNullOrWhiteSpace(book.Summary)) meta.Add(new XElement(dc + "description", book.Summary.Trim()));
        foreach (var g in book.Genre.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            meta.Add(new XElement(dc + "subject", g));

        var manifest = new XElement(ns + "manifest",
            new XElement(ns + "item", new XAttribute("id", "nav"), new XAttribute("href", "nav.xhtml"), new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav")),
            new XElement(ns + "item", new XAttribute("id", "ncx"), new XAttribute("href", "toc.ncx"), new XAttribute("media-type", "application/x-dtbncx+xml")));
        var spine = new XElement(ns + "spine", new XAttribute("toc", "ncx"),
            new XAttribute("page-progression-direction", book.RightToLeft ? "rtl" : "ltr"));
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            var img = new XElement(ns + "item", new XAttribute("id", i == 0 ? "cover" : "img-" + p.Id),
                new XAttribute("href", $"Images/{p.Id}{p.Extension}"), new XAttribute("media-type", p.MediaType));
            if (i == 0) img.Add(new XAttribute("properties", "cover-image"));
            manifest.Add(new XElement(ns + "item", new XAttribute("id", p.Id), new XAttribute("href", $"Text/{p.Id}.xhtml"), new XAttribute("media-type", "application/xhtml+xml")), img);
            spine.Add(new XElement(ns + "itemref", new XAttribute("idref", p.Id)));
        }

        var opf = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "package", new XAttribute("version", "3.0"), new XAttribute("unique-identifier", "bookid"),
                new XAttribute("prefix", "rendition: http://www.idpf.org/vocab/rendition/#"),
                meta, manifest, spine));
        return ToXml(opf);
    }

    // Una pagina = un'immagine a tutto schermo, con viewport grande quanto l'immagine
    private static string PageXhtml(Page p) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head>
        <title>{{p.Id}}</title>
        <meta name="viewport" content="width={{p.Width}}, height={{p.Height}}"/>
        <style>html, body { margin: 0; padding: 0; } img { display: block; }</style>
        </head>
        <body><img src="../Images/{{p.Id}}{{p.Extension}}" width="{{p.Width}}" height="{{p.Height}}" alt=""/></body>
        </html>
        """;

    // Indice (EPUB 3): una voce per capitolo, che porta alla sua prima pagina
    private static string Nav(string title, List<(string Label, string PageId)> toc) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head><title>{Esc(title)}</title></head>
        <body><nav epub:type="toc"><ol>{string.Concat(toc.Select(t => $"<li><a href=\"Text/{t.PageId}.xhtml\">{Esc(t.Label)}</a></li>"))}</ol></nav></body>
        </html>
        """;

    // Stesso indice nel formato EPUB 2, letto da alcuni lettori
    private static string Ncx(string title, string id, List<(string Label, string PageId)> toc) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
        <head><meta name="dtb:uid" content="{Esc(id)}"/></head>
        <docTitle><text>{Esc(title)}</text></docTitle>
        <navMap>{string.Concat(toc.Select((t, i) => $"<navPoint id=\"n{i + 1}\" playOrder=\"{i + 1}\"><navLabel><text>{Esc(t.Label)}</text></navLabel><content src=\"Text/{t.PageId}.xhtml\"/></navPoint>"))}</navMap>
        </ncx>
        """;

    private const string ContainerXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
        <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
        </container>
        """;

    // Identificativo stabile ricavato dal testo dato: ricreare lo stesso libro non lo duplica
    private static string Identifier(string key) => "urn:uuid:" + new Guid(MD5.HashData(Encoding.UTF8.GetBytes(key)));

    // "2" -> "02": i volumi restano in ordine anche alfabeticamente
    private static string PadVolume(string s)
    {
        s = NormalizeNumber(s);
        return s.All(char.IsDigit) ? s.PadLeft(2, '0') : s;
    }

    private static void Add(ZipArchive zip, string name, string text, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var s = zip.CreateEntry(name, level).Open();
        s.Write(new UTF8Encoding(false).GetBytes(text));
    }

    private static string ToXml(XDocument doc)
    {
        using var sw = new Utf8StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }

    private static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? "";

    private static double ParseNumber(string s) =>
        double.TryParse(NormalizeNumber(s), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.MaxValue;

    // "012" -> "12", "12,5" -> "12.5"
    private static string NormalizeNumber(string s)
    {
        s = s.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d.ToString(CultureInfo.InvariantCulture) : s;
    }

    // "12" -> "012", "12.5" -> "012.5": i titoli restano in ordine anche alfabeticamente
    internal static string PadNumber(string s)
    {
        s = NormalizeNumber(s);
        int dot = s.IndexOf('.');
        string whole = dot < 0 ? s : s[..dot];
        return whole.All(char.IsDigit) && whole.Length > 0 ? whole.PadLeft(3, '0') + (dot < 0 ? "" : s[dot..]) : s;
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?(?!.*\d)")]
    private static partial Regex LastNumber();
}

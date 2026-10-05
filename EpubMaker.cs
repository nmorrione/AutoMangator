using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace AutoMangator;

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
/// Trasforma un .cbz in un .kepub.epub a layout fisso per Kobo, con titolo, autore, serie
/// e numero del capitolo nei metadati. Le immagini restano quelle originali; solo i formati
/// che Kobo non mostra (WebP, AVIF) vengono convertiti in JPEG.
/// </summary>
public static partial class EpubMaker
{
    public const string OutputFolderName = "Kobo";

    /// <summary>Elenca i .cbz della cartella, con il numero preso dal nome del file.</summary>
    public static List<ChapterInfo> LoadChapters(string folder)
    {
        var chapters = Directory.GetFiles(folder, "*.cbz")
            .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
            .Select(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                var m = LastNumber().Match(name);
                return new ChapterInfo { CbzPath = f, Number = m.Success ? NormalizeNumber(m.Value) : "", Title = name };
            }).ToList();

        // I file senza numero nel nome vanno in coda, dopo il numero più alto
        double max = chapters.Where(c => c.Number != "").Select(c => ParseNumber(c.Number)).DefaultIfEmpty(0).Max();
        int next = (int)Math.Floor(max) + 1;
        foreach (var c in chapters.Where(c => c.Number == ""))
            c.Number = (next++).ToString(CultureInfo.InvariantCulture);

        return chapters.OrderBy(c => ParseNumber(c.Number)).ThenBy(c => c.FileName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Rilegge i dati comuni da un EPUB già creato in precedenza, se c'è.</summary>
    public static BookInfo? TryReadExisting(string folder)
    {
        string kobo = Path.Combine(folder, OutputFolderName);
        string? epub = Directory.Exists(kobo) ? Directory.GetFiles(kobo, "*.epub").Order().FirstOrDefault() : null;
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
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { return null; }
    }

    /// <summary>Titolo mostrato dal Kobo: "Serie 012 - Titolo", così i capitoli restano in ordine.</summary>
    public static string BookTitle(BookInfo book, ChapterInfo ch)
    {
        string title = $"{book.Series} {PadNumber(ch.Number)}";
        return string.IsNullOrWhiteSpace(ch.Title) ? title : $"{title} - {ch.Title.Trim()}";
    }

    /// <summary>Crea l'EPUB e restituisce il percorso del file.</summary>
    public static string Create(BookInfo book, ChapterInfo ch, string outputFolder)
    {
        var pages = ReadPages(ch.CbzPath);
        if (pages.Count == 0)
            throw new InvalidOperationException($"Nessuna immagine in {ch.FileName}.");

        Directory.CreateDirectory(outputFolder);
        string outPath = Path.Combine(outputFolder, CbzMaker.SafeFileName($"{book.Series} {PadNumber(ch.Number)}") + ".kepub.epub");
        string tmpPath = outPath + ".part";
        try
        {
            using (var zip = ZipFile.Open(tmpPath, ZipArchiveMode.Create))
            {
                // "mimetype" deve essere il primo file e non compresso
                Add(zip, "mimetype", "application/epub+zip", CompressionLevel.NoCompression);
                Add(zip, "META-INF/container.xml", ContainerXml);
                Add(zip, "OEBPS/content.opf", Opf(book, ch, pages));
                Add(zip, "OEBPS/nav.xhtml", Nav(BookTitle(book, ch)));
                Add(zip, "OEBPS/toc.ncx", Ncx(BookTitle(book, ch), Identifier(book, ch)));
                foreach (var p in pages)
                {
                    Add(zip, $"OEBPS/Text/{p.Id}.xhtml", PageXhtml(p));
                    var entry = zip.CreateEntry($"OEBPS/Images/{p.Id}{p.Extension}", CompressionLevel.NoCompression);
                    using var s = entry.Open();
                    s.Write(p.Data);
                }
            }
            File.Move(tmpPath, outPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
        return outPath;
    }

    private sealed record Page(string Id, byte[] Data, string Extension, string MediaType, int Width, int Height);

    private static List<Page> ReadPages(string cbzPath)
    {
        using var zip = ZipFile.OpenRead(cbzPath);
        var pages = new List<Page>();
        foreach (var entry in zip.Entries.Where(e => e.Length > 0).OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase))
        {
            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            byte[] data = ms.ToArray();

            using var codec = SKCodec.Create(new MemoryStream(data));
            if (codec is null) continue; // non è un'immagine (es. ComicInfo.xml)

            string id = $"p{pages.Count + 1:D4}";
            var (w, h) = (codec.Info.Width, codec.Info.Height);
            switch (codec.EncodedFormat)
            {
                case SKEncodedImageFormat.Jpeg: pages.Add(new(id, data, ".jpg", "image/jpeg", w, h)); break;
                case SKEncodedImageFormat.Png: pages.Add(new(id, data, ".png", "image/png", w, h)); break;
                case SKEncodedImageFormat.Gif: pages.Add(new(id, data, ".gif", "image/gif", w, h)); break;
                default:
                    // WebP, AVIF, ...: Kobo non li mostra negli EPUB
                    using (var bmp = SKBitmap.Decode(data))
                    using (var img = SKImage.FromBitmap(bmp))
                    using (var jpg = img.Encode(SKEncodedImageFormat.Jpeg, 92))
                        pages.Add(new(id, jpg.ToArray(), ".jpg", "image/jpeg", bmp.Width, bmp.Height));
                    break;
            }
        }
        return pages;
    }

    private static string Opf(BookInfo book, ChapterInfo ch, List<Page> pages)
    {
        XNamespace ns = "http://www.idpf.org/2007/opf", dc = "http://purl.org/dc/elements/1.1/";
        string index = NormalizeNumber(ch.Number);
        var meta = new XElement(ns + "metadata", new XAttribute(XNamespace.Xmlns + "dc", dc),
            new XElement(dc + "identifier", new XAttribute("id", "bookid"), Identifier(book, ch)),
            new XElement(dc + "title", BookTitle(book, ch)),
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

        // Il primo autore è quello che Kobo mostra in libreria
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

    private static string Nav(string title) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
        <head><title>{Esc(title)}</title></head>
        <body><nav epub:type="toc"><ol><li><a href="Text/p0001.xhtml">{Esc(title)}</a></li></ol></nav></body>
        </html>
        """;

    private static string Ncx(string title, string id) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <ncx xmlns="http://www.daisy.org/z3986/2005/ncx/" version="2005-1">
        <head><meta name="dtb:uid" content="{Esc(id)}"/></head>
        <docTitle><text>{Esc(title)}</text></docTitle>
        <navMap><navPoint id="p1" playOrder="1"><navLabel><text>{Esc(title)}</text></navLabel><content src="Text/p0001.xhtml"/></navPoint></navMap>
        </ncx>
        """;

    private const string ContainerXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
        <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
        </container>
        """;

    // Stesso identificativo per lo stesso capitolo: ricreare l'EPUB non lo duplica
    private static string Identifier(BookInfo book, ChapterInfo ch)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{book.Series.Trim().ToLowerInvariant()}|{NormalizeNumber(ch.Number)}"));
        return "urn:uuid:" + new Guid(hash);
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
    private static string PadNumber(string s)
    {
        s = NormalizeNumber(s);
        int dot = s.IndexOf('.');
        string whole = dot < 0 ? s : s[..dot];
        return whole.All(char.IsDigit) && whole.Length > 0 ? whole.PadLeft(3, '0') + (dot < 0 ? "" : s[dot..]) : s;
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?(?!.*\d)")]
    private static partial Regex LastNumber();
}

#:package Microsoft.Playwright@1.63.0
#:property PublishAot=false

// Genera icon.ico (Windows, a tutta superficie), AppIcon.icns (macOS, griglia Apple con margine)
// e icon.png (finestra) partendo da icon.svg, disegnando ogni dimensione dal vettoriale.
// Uso (dalla cartella Assets):  dotnet run make-icons.cs -- .
using Microsoft.Playwright;

string assets = args[0];
string svg = File.ReadAllText(Path.Combine(assets, "icon.svg"));

using var pw = await Playwright.CreateAsync();
await using var browser = await pw.Chromium.LaunchAsync(new() { Channel = "msedge" });
var page = await browser.NewPageAsync();

async Task<byte[]> Render(int size, bool macGrid)
{
    // macOS: corpo dell'icona 824/1024 centrato (griglia Apple), il resto trasparente
    string inner = macGrid
        ? $"<div style='width:{size * 824 / 1024.0}px;height:{size * 824 / 1024.0}px;margin:{size * 100 / 1024.0}px'>{svg}</div>"
        : $"<div style='width:{size}px;height:{size}px'>{svg}</div>";
    await page.SetViewportSizeAsync(size, size);
    await page.SetContentAsync($"<html><body style='margin:0;background:transparent'>{inner}<style>svg{{width:100%;height:100%;display:block}}</style></body></html>");
    return await page.ScreenshotAsync(new() { OmitBackground = true, Clip = new() { X = 0, Y = 0, Width = size, Height = size } });
}

// ICO con voci PNG (supportate da Windows Vista in poi)
int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
var icoImages = new List<byte[]>();
foreach (var s in icoSizes) icoImages.Add(await Render(s, false));
using (var fs = File.Create(Path.Combine(assets, "icon.ico")))
using (var w = new BinaryWriter(fs))
{
    w.Write((short)0); w.Write((short)1); w.Write((short)icoSizes.Length);
    int offset = 6 + 16 * icoSizes.Length;
    for (int i = 0; i < icoSizes.Length; i++)
    {
        byte dim = (byte)(icoSizes[i] >= 256 ? 0 : icoSizes[i]);
        w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32);
        w.Write(icoImages[i].Length); w.Write(offset);
        offset += icoImages[i].Length;
    }
    foreach (var img in icoImages) w.Write(img);
}

// ICNS con voci PNG
(string type, int size)[] icns =
[
    ("icp4", 16), ("icp5", 32), ("icp6", 64), ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
    ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512),
];
var chunks = new List<(string type, byte[] data)>();
foreach (var (type, size) in icns) chunks.Add((type, await Render(size, true)));
using (var fs = File.Create(Path.Combine(assets, "AppIcon.icns")))
{
    void BE(int v) { fs.Write([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]); }
    fs.Write("icns"u8); BE(8 + chunks.Sum(c => 8 + c.data.Length));
    foreach (var (type, data) in chunks) { fs.Write(System.Text.Encoding.ASCII.GetBytes(type)); BE(8 + data.Length); fs.Write(data); }
}

// PNG per l'icona della finestra
File.WriteAllBytes(Path.Combine(assets, "icon.png"), await Render(256, false));
Console.WriteLine("Icone generate.");

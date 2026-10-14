using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Web2Comic;

/// <summary>
/// Traduzioni dell'interfaccia. I testi stanno in Strings/&lt;lingua&gt;.json, inclusi nell'exe:
/// per aggiungere una lingua basta un nuovo file JSON e una voce in <see cref="Languages"/>.
/// Cambiando lingua l'interfaccia si aggiorna subito, senza riavviare.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    /// <summary>Lingue disponibili: codice e nome nella lingua stessa.</summary>
    public static readonly (string Code, string Name)[] Languages = [("it", "Italiano"), ("en", "English"), ("es", "Español")];

    private const string Fallback = "en";
    private static readonly Dictionary<string, Dictionary<string, string>> Cache = [];

    // Dopo i campi statici qui sopra: si inizializzano nell'ordine in cui sono scritti,
    // e il costruttore li usa già
    public static Loc Instance { get; } = new();

    private Dictionary<string, string> _strings = [];

    public string Language { get; private set; } = Fallback;

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Per i testi impostati da codice (es. l'etichetta del pulsante principale).</summary>
    public event Action? LanguageChanged;

    private Loc() => SetLanguage(SystemLanguage());

    /// <summary>La lingua del sistema, se è tra quelle disponibili; altrimenti inglese.</summary>
    private static string SystemLanguage()
    {
        string sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Languages.Any(l => l.Code == sys) ? sys : Fallback;
    }

    public void SetLanguage(string code)
    {
        Language = code;
        _strings = Load(code);
        // Aggiorna tutti i testi legati con {local:Tr ...}
        foreach (var text in _texts.Values) text.Refresh();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke();
    }

    public string this[string key] => Get(_strings, key);

    // Un oggetto per chiave, a cui si legano i testi dello XAML: notifica il cambio di "Value"
    // quando cambia la lingua (più affidabile del binding all'indicizzatore)
    private readonly Dictionary<string, LocText> _texts = [];

    internal LocText Text(string key)
    {
        if (!_texts.TryGetValue(key, out var text)) _texts[key] = text = new LocText(key);
        return text;
    }

    /// <summary>Testo tradotto nella lingua dell'interfaccia.</summary>
    public static string T(string key) => Instance[key];

    /// <summary>Testo tradotto con segnaposto {0}, {1}, ...</summary>
    public static string T(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, Instance[key], args);

    /// <summary>Testo in una lingua precisa (es. quella del libro), con ripiego sull'inglese.</summary>
    public static string In(string language, string key, params object?[] args)
    {
        string code = Languages.Any(l => l.Code == language) ? language : Fallback;
        return string.Format(CultureInfo.InvariantCulture, Get(Load(code), key), args);
    }

    private static string Get(Dictionary<string, string> strings, string key) =>
        strings.TryGetValue(key, out var s) ? s
        : Load(Fallback).TryGetValue(key, out var f) ? f
        : key; // chiave mancante: meglio vederla che un testo vuoto

    private static Dictionary<string, string> Load(string code)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(code, out var cached)) return cached;
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"Web2Comic.Strings.{code}.json");
            var strings = stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            Cache[code] = strings;
            return strings;
        }
    }
}

/// <summary>
/// Testo tradotto nello XAML: Text="{local:Tr Main.Start}". Resta legato alla lingua corrente,
/// quindi cambia da solo quando si sceglie un'altra lingua.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding(nameof(LocText.Value)) { Source = Loc.Instance.Text(Key), Mode = BindingMode.OneWay };
}

/// <summary>Un testo tradotto, legabile dallo XAML; cambia valore quando cambia la lingua.</summary>
public sealed class LocText(string key) : INotifyPropertyChanged
{
    public string Value => Loc.T(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}

# Web2Comic per sviluppatori

> Questa pagina è in italiano. / This page is in Italian: the app and the [README](../README.md) are also in English and Spanish.

App desktop in C# (.NET 10) con interfaccia [Avalonia UI](https://avaloniaui.net/) e [Playwright](https://playwright.dev/dotnet/)
per pilotare il browser: Microsoft Edge su Windows, WebKit (scaricato al primo avvio) su macOS.

## Struttura
| File | Cosa contiene |
|---|---|
| `CbzMaker.cs` | Apre la pagina, la scorre, raccoglie le immagini e scrive il `.cbz` |
| `CbzMaker.Reader.cs` | Reader a una pagina per volta, protezione da popup/navigazioni, verifiche anti-bot |
| `CbzMaker.List.cs` | Dalla pagina con la lista dei capitoli ricava gli indirizzi (gruppo di link con lo stesso schema) |
| `EpubMaker.cs` | Da `.cbz` a EPUB 3 a layout fisso con i metadati della serie, anche come volume con indice (`.kepub.epub` se ottimizzato per Kobo: cambia solo l'estensione) |
| `MainWindow.axaml(.cs)` | Finestra principale |
| `ConvertWindow.axaml(.cs)` | Finestra "Converti CBZ in EPUB" per una cartella di `.cbz` |
| `SeriesInfoPanel.axaml(.cs)` | Campi della serie, condivisi dalle due finestre |
| `Loc.cs` | Traduzioni: carica `Strings/<lingua>.json`, `{local:Tr Chiave}` nello XAML e `Loc.T("Chiave")` nel codice |
| `Strings/*.json` | Testi dell'interfaccia in italiano, inglese e spagnolo |
| `Assets/` | Icona e bandierine: `icon.svg` e `flags/*.svg` sono le sorgenti, i PNG si rigenerano con `make-icons.cs` |

## Traduzioni
I testi stanno in `Strings/it.json`, `en.json` ed `es.json` (chiave → testo, con segnaposto `{0}`, `{1}`...),
inclusi nell'exe. Non si usano i `.resx` perché creano una cartella per lingua, che dentro l'app macOS
romperebbe la firma del bundle. All'avvio si usa la lingua del sistema se disponibile, altrimenti l'inglese.

Per aggiungere una lingua:
1. copia `Strings/en.json` in `Strings/<codice>.json` (es. `fr.json`) e traduci i testi, lasciando uguali chiavi e segnaposto;
2. aggiungi la lingua a `Loc.Languages` in `Loc.cs`;
3. aggiungi la bandierina `Assets/flags/<codice>.svg`, rigenera i PNG con `make-icons.cs` e aggiungi il pulsante in `MainWindow.axaml`.

## Compilare
Serve il .NET 10 SDK.

Exe singolo portable per Windows (runtime .NET incluso) in `publish-portable\`:
```
dotnet publish Web2Comic.csproj -p:PublishProfile=Portable
```

## Da terminale
- `Web2Comic --cli <indirizzo> [cartella] [--reader] [--show]`: crea il `.cbz` senza aprire la finestra
  (`--reader` forza lo sfoglio del reader, `--show` mostra il browser).
- `Web2Comic --list <indirizzo della lista> [--show]`: stampa i capitoli trovati (numero, indirizzo, titolo),
  come "Trova capitoli".
- `Web2Comic --epub <cartella> <serie> [autore] [--kobo]`: crea gli EPUB dai `.cbz` della cartella, nella sottocartella `EPUB`
  (`--kobo` li crea come `.kepub.epub`).

## Icone
Dopo aver modificato `Assets/icon.svg`, dalla cartella `Assets`:
```
dotnet run make-icons.cs -- .
```
Rigenera `icon.ico` (Windows), `AppIcon.icns` (macOS) e `icon.png` (finestra).

## Nuove versioni
Le Release si pubblicano da sole con GitHub Actions ([build.yml](../.github/workflows/build.yml)):
aggiornare `<Version>` nel `.csproj`, poi creare e caricare un tag:
```
git tag v1.5.0
git push origin v1.5.0
```
GitHub compila la versione Windows e quella macOS (firmata ad-hoc), le prova su una pagina vera (anche la creazione
degli EPUB) e crea la Release con entrambi i file e le note di [release-notes.md](../.github/release-notes.md).

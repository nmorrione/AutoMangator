# AutoMangator per sviluppatori

App desktop in C# (.NET 10) con interfaccia [Avalonia UI](https://avaloniaui.net/) e [Playwright](https://playwright.dev/dotnet/)
per pilotare il browser: Microsoft Edge su Windows, WebKit (scaricato al primo avvio) su macOS.

## Struttura
| File | Cosa contiene |
|---|---|
| `CbzMaker.cs` | Apre la pagina, la scorre, raccoglie le immagini e scrive il `.cbz` |
| `CbzMaker.Reader.cs` | Reader a una pagina per volta, protezione da popup/navigazioni, verifiche anti-bot |
| `CbzMaker.List.cs` | Dalla pagina con la lista dei capitoli ricava gli indirizzi (gruppo di link con lo stesso schema) |
| `EpubMaker.cs` | Da `.cbz` a `.kepub.epub` a layout fisso con i metadati della serie, anche come volume con indice |
| `MainWindow.axaml(.cs)` | Finestra principale |
| `KoboWindow.axaml(.cs)` | Finestra "EPUB per Kobo" per una cartella di `.cbz` |
| `SeriesInfoPanel.axaml(.cs)` | Campi della serie, condivisi dalle due finestre |
| `Assets/` | Icona: `icon.svg` è la sorgente, il resto si rigenera con `make-icons.cs` |

## Compilare
Serve il .NET 10 SDK.

Exe singolo portable per Windows (runtime .NET incluso) in `publish-portable\`:
```
dotnet publish AutoMangator.csproj -p:PublishProfile=Portable
```

## Da terminale
- `AutoMangator --cli <indirizzo> [cartella] [--reader] [--show]`: crea il `.cbz` senza aprire la finestra
  (`--reader` forza lo sfoglio del reader, `--show` mostra il browser).
- `AutoMangator --epub <cartella> <serie> [autore]`: crea gli EPUB per Kobo dai `.cbz` della cartella.

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

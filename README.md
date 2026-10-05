# AutoMangator

Piccola app per **Windows e macOS** che trasforma una pagina web piena di immagini (ad esempio il capitolo di un fumetto) in un file **.cbz** da leggere sull'ebook reader.

Al posto del vecchio flusso manuale (Ctrl+S della pagina → copia delle sole immagini → zip senza compressione → rinomina in .cbz) basta incollare l'indirizzo e premere **Crea CBZ**.

## Download
Le versioni pronte sono nelle [Release](https://github.com/nmorrione/AutoMangator/releases/latest):
- **Windows**: `AutoMangator-Windows-x64.exe`, un unico file portable, non serve installare nulla.
- **macOS** (Apple Silicon): `AutoMangator-macOS-AppleSilicon.zip`, da estrarre e spostare in Applicazioni.
  L'app non è firmata da Apple: al primo avvio va sbloccata da **Impostazioni di Sistema → Privacy e sicurezza → Apri comunque**
  (oppure `xattr -dr com.apple.quarantine /Applications/AutoMangator.app`).

## Come funziona
1. Apre la pagina in modo invisibile tramite [Playwright](https://playwright.dev/dotnet/):
   su Windows con Microsoft Edge (già presente), su macOS con WebKit, il motore di Safari (scaricato automaticamente al primo avvio).
2. Scorre la pagina fino in fondo, così le immagini caricate "pigramente" vengono scaricate.
3. Prende le immagini nell'ordine in cui compaiono, scartando quelle piccole (loghi, icone, banner) e i formati non adatti (SVG).
4. Le salva in uno zip **senza compressione**, con nomi `001.jpg`, `002.png`, … ed estensione `.cbz`.

## Uso
- **Indirizzi**: uno per riga; ogni indirizzo diventa un file .cbz.
- **Nome file**: opzionale; vuoto = titolo della pagina. Con più indirizzi viene numerato.
- **Cartella di destinazione**: di default la cartella Download.
- **Lato minimo immagine**: sotto questa dimensione (px) un'immagine viene scartata.
- **Mostra il browser**: utile se il sito blocca i browser invisibili o chiede una verifica.

## EPUB per Kobo
Il software di serie dei Kobo non legge i dati contenuti nei `.cbz` (mostra il nome del file e "Autore sconosciuto").
Con **EPUB per Kobo...** si sceglie la cartella con i `.cbz` dei capitoli e si inseriscono una volta sola i dati della serie
(serie, autore, disegnatore, editore, anno, genere, lingua, trama, lettura da destra a sinistra).
Per ogni capitolo viene creato, nella sottocartella `Kobo`, un `.kepub.epub` a layout fisso con quei dati:
- titolo "Serie 012 - Titolo del capitolo", così i capitoli restano in ordine in libreria;
- numero del capitolo preso dal nome del file (correggibile nella tabella); i file senza numero vanno in coda;
- immagini originali, senza ricompressione (solo WebP e AVIF, che Kobo non mostra, diventano JPEG);
- serie e numero anche nei metadati: il Kobo li mostra nella scheda Serie se è installato [NickelSeries](https://pgaskin.net/kepubify/ns/).

Basta copiare gli EPUB nel Kobo collegato via USB. Riaprendo la stessa cartella, i dati della serie vengono riproposti.

## Da terminale
- `AutoMangator --cli <indirizzo> [cartella]`: crea il `.cbz` senza aprire la finestra.
- `AutoMangator --epub <cartella> <serie> [autore]`: crea gli EPUB per Kobo dai `.cbz` della cartella.

## Compilare
Serve il .NET 10 SDK. L'interfaccia è fatta con [Avalonia UI](https://avaloniaui.net/).

Exe singolo portable per Windows (runtime .NET incluso) in `publish-portable\`:
```
dotnet publish AutoMangator.csproj -p:PublishProfile=Portable
```

## Nuove versioni
Le Release si pubblicano da sole con GitHub Actions ([build.yml](.github/workflows/build.yml)):
aggiornare `<Version>` nel `.csproj`, poi creare e caricare un tag:
```
git tag v1.2.0
git push origin v1.2.0
```
GitHub compila la versione Windows e quella macOS, le prova su una pagina vera e crea la Release con entrambi i file.

## Licenza
[MIT](LICENSE)

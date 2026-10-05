# AutoMangator

Piccola app Windows che trasforma una pagina web piena di immagini (ad esempio il capitolo di un fumetto) in un file **.cbz** da leggere sull'ebook reader.

Al posto del vecchio flusso manuale (Ctrl+S della pagina → copia delle sole immagini → zip senza compressione → rinomina in .cbz) basta incollare l'indirizzo e premere **Crea CBZ**.

## Come funziona
1. Apre la pagina in Microsoft Edge (già presente in Windows) in modalità invisibile, tramite [Playwright](https://playwright.dev/dotnet/).
2. Scorre la pagina fino in fondo, così le immagini caricate "pigramente" vengono scaricate.
3. Prende le immagini nell'ordine in cui compaiono, scartando quelle piccole (loghi, icone, banner) e i formati non adatti (SVG).
4. Le salva in uno zip **senza compressione**, con nomi `001.jpg`, `002.png`, … ed estensione `.cbz`.

## Uso
- **Indirizzi**: uno per riga; ogni indirizzo diventa un file .cbz.
- **Nome file**: opzionale; vuoto = titolo della pagina. Con più indirizzi viene numerato.
- **Cartella di destinazione**: di default la cartella Download.
- **Lato minimo immagine**: sotto questa dimensione (px) un'immagine viene scartata.
- **Mostra il browser**: utile se il sito blocca i browser invisibili o chiede una verifica.

## Requisiti
- Windows 10/11 con Microsoft Edge
- Per compilare: .NET 10 SDK

## Compilare
Exe singolo portable (runtime .NET incluso) in `publish-portable\`:
```
dotnet publish AutoMangator.csproj -p:PublishProfile=Portable
```
Versione a cartella, più leggera (richiede il runtime .NET 10) in `publish\`:
```
dotnet publish AutoMangator.csproj -p:PublishProfile=FolderProfile
```

<p align="center">
  <a href="README.md">🇬🇧 English</a> &nbsp;·&nbsp; <b>🇮🇹 Italiano</b> &nbsp;·&nbsp; <a href="README.es.md">🇪🇸 Español</a>
</p>

<p align="center">
  <img src="Assets/icon.png" width="128" alt="Web2Comic">
</p>

<h1 align="center">Web2Comic</h1>

<p align="center">
  <b>Dalla pagina web al tuo ebook reader, in un clic.</b><br>
  Incolla l'indirizzo di un capitolo: Web2Comic raccoglie le pagine e crea un fumetto <code>.cbz</code> o un <code>EPUB</code> con titolo, serie e autore.
</p>

<p align="center">
  <a href="https://github.com/nmorrione/Web2Comic/releases/latest"><img src="https://img.shields.io/github/v/release/nmorrione/Web2Comic?label=versione&color=e5245e" alt="Ultima versione"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4" alt="Windows 10 e 11">
  <img src="https://img.shields.io/badge/macOS-Apple%20Silicon-555555" alt="macOS Apple Silicon">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/nmorrione/Web2Comic?label=licenza&color=ff8a3d" alt="Licenza MIT"></a>
</p>

<p align="center">
  <a href="https://github.com/nmorrione/Web2Comic/releases/latest/download/Web2Comic-Windows-x64.exe"><b>⬇ Scarica per Windows</b></a>
  &nbsp;·&nbsp;
  <a href="https://github.com/nmorrione/Web2Comic/releases/latest/download/Web2Comic-macOS-AppleSilicon.zip"><b>⬇ Scarica per Mac</b></a>
</p>

<p align="center">
  <img src="docs/screenshot-it.png" width="560" alt="La finestra di Web2Comic">
</p>

## Cosa fa
- 📖 **Capitolo intero in un file**: apre la pagina, la scorre fino in fondo e raccoglie le immagini nell'ordine giusto.
- 🔁 **Funziona anche con i reader a una pagina per volta**: se ne accorge da solo e li sfoglia fino all'ultima pagina.
- 📋 **Dalla lista dei capitoli**: incolla la pagina che elenca i capitoli di una serie, scegli dal/al e gli indirizzi si compilano da soli.
- 📦 **Più capitoli in coda**: un indirizzo per riga, un file per capitolo, oppure...
- 📕 **...un unico volume**: tutti i capitoli in un solo file, con l'indice per saltare da un capitolo all'altro.
- ⚡ **Più capitoli in parallelo**: fino a 6 capitoli insieme, circa il doppio più veloce; se il sito ha un intoppo momentaneo, il capitolo viene riprovato da solo.
- 📚 **EPUB con i dati della serie**: titolo, serie, numero del capitolo, autore e trama compaiono nella libreria dell'ebook reader. Con un clic, ottimizzato per **Kobo**.
- 🖼️ **Immagini originali**: nessuna ricompressione, la qualità resta quella del sito.
- 🛡️ **Niente pubblicità**: popup e nuove schede vengono chiusi, e la pagina non può essere portata su altri siti.
- 🌍 **In italiano, inglese e spagnolo**: parte nella lingua del sistema e si cambia al volo con le bandierine in alto.
- 💼 **Portable**: un unico file da avviare, niente installazione.

## Come si usa
1. Incolla l'indirizzo del capitolo (o più indirizzi, uno per riga).
2. Scegli la cartella dove salvare.
3. Premi **Crea CBZ**. Fatto!

**Tanti capitoli?** Incolla in **Lista capitoli** la pagina del sito che li elenca tutti, indica **dal** / **al** (vuoti = tutti)
e premi **Trova capitoli**: gli indirizzi finiscono in **Indirizzi**, dove puoi controllarli prima di partire.

**Un volume unico?** Spunta **Crea singolo volume**: i capitoli vengono uniti in un solo file, chiamato per esempio
"La mia serie - Capitoli 001-010" (oppure come scrivi in **Nome file**). Se qualche capitolo non si riesce a scaricare,
l'app te lo dice e ti chiede se creare comunque il volume con gli altri.

**Un EPUB invece del CBZ?** Spunta **Crea EPUB**, scrivi il nome della serie (ed eventualmente autore, genere, trama...)
e copia gli EPUB nell'ebook reader via USB. Con il volume puoi indicare anche il **Numero volume**: vedrai "La mia serie Vol. 01".
Se leggi su un **Kobo**, spunta anche **Ottimizzato per Kobo**.

### CBZ o EPUB? E per il Kobo?
| Formato | Per chi |
|---|---|
| **CBZ** | Il formato classico dei fumetti: lo leggono i Kobo, PocketBook, molte app per tablet e PC. Contiene solo le pagine, senza titolo e autore. |
| **EPUB** | Standard per tutti gli ebook reader (Kobo, PocketBook, Tolino, Apple Books, app Android...), con titolo, serie, autore, trama e indice. |
| **EPUB + Ottimizzato per Kobo** | Stesso EPUB, ma chiamato `.kepub.epub`: così il Kobo lo apre col **suo** lettore, che mostra le pagine dei fumetti a schermo intero ed è più veloce a girare pagina. Gli altri ebook reader non lo riconoscono. |

**Hai un Kindle?** Il Kindle non legge né CBZ né EPUB a pagine fisse. Il modo più semplice è passare i `.cbz` creati da Web2Comic a
[Kindle Comic Converter](https://github.com/ciromattia/kcc), un programma gratuito pensato apposta per i fumetti su Kindle.

### Cosa spuntare
Di solito **niente**: Web2Comic capisce da solo se la pagina mostra tutto il capitolo o è un reader da sfogliare.

| Casella | Quando serve |
|---|---|
| **Mostra il browser** | Il sito ha una protezione anti-bot. Te lo dice l'app e spunta la casella per te: premi di nuovo il pulsante e, se nella finestra compare una verifica, completala tu. |
| **Sfoglia il reader pagina per pagina** | È un reader a una pagina per volta, ma ti è arrivata solo la prima pagina. |
| **Crea EPUB** | Vuoi gli EPUB con i dati della serie invece del semplice `.cbz`. |
| **Ottimizzato per Kobo** | Leggi gli EPUB su un Kobo. |
| **Crea singolo volume** | Vuoi tutti i capitoli in un unico file invece di uno per capitolo. |
| **Scarica più capitoli in parallelo** | Scarichi tanti capitoli e vuoi fare prima (3 alla volta è un buon valore; se il sito inizia a dare errori, abbassalo). |

Hai già una cartella piena di `.cbz`? Il pulsante **Converti CBZ in EPUB...** li trasforma tutti in una volta, senza scaricare nulla, con una tabella per correggere numeri e titoli dei capitoli.

## Installazione
**Windows 10/11**: scarica `Web2Comic-Windows-x64.exe` e avvialo. Usa Microsoft Edge, già presente in Windows.
Se compare "Windows ha protetto il PC", clicca **Ulteriori informazioni → Esegui comunque**: l'app non è firmata con un certificato a pagamento.

**Mac (Apple Silicon)**: scarica lo zip, estrailo e sposta `Web2Comic.app` in **Applicazioni**.
Al primo avvio macOS la blocca perché non è firmata da Apple: vai in **Impostazioni di Sistema → Privacy e sicurezza → Apri comunque**.
Alla prima conversione scarica WebKit, il motore di Safari (una volta sola).

## Domande frequenti
<details>
<summary><b>Il Kobo mostra "Autore sconosciuto" e il nome del file</b></summary>

È normale con i `.cbz`: il software del Kobo non legge i dati al loro interno. Usa **Crea EPUB** con **Ottimizzato per Kobo** e vedrai titolo, autore e copertina.
La scheda **Serie** del Kobo, per i libri copiati via USB, richiede la mod gratuita [NickelSeries](https://pgaskin.net/kepubify/ns/).
</details>

<details>
<summary><b>Sul Kobo le pagine del fumetto non sono a schermo intero</b></summary>

Probabilmente l'EPUB è stato creato senza **Ottimizzato per Kobo**: con l'estensione `.epub` il Kobo usa il suo lettore più vecchio,
che gestisce male le pagine a dimensione fissa. Ricrealo con la casella spuntata (anche dai `.cbz`, con **Converti CBZ in EPUB...**).
</details>

<details>
<summary><b>Mi è arrivata solo la prima pagina</b></summary>

Il sito usa un reader che Web2Comic non ha riconosciuto: spunta **Sfoglia il reader pagina per pagina** e riprova.
Se ancora non funziona, [apri una segnalazione](https://github.com/nmorrione/Web2Comic/issues) con l'indirizzo del sito.
</details>

<details>
<summary><b>Nel file finiscono loghi o banner</b></summary>

Alza **Lato minimo immagine**: le immagini più piccole di quel valore vengono scartate. Se invece mancano pagine, abbassalo.
</details>

<details>
<summary><b>È gratis?</b></summary>

Sì, ed è open source con licenza MIT. Usalo solo per contenuti che hai il diritto di scaricare.
</details>

---

<p align="center">
  <sub>Sei uno sviluppatore? Trovi come compilarlo e come funziona in <a href="docs/SVILUPPO.md">docs/SVILUPPO.md</a> · Licenza <a href="LICENSE">MIT</a></sub>
</p>

## Download
- **Windows** (10/11, 64 bit): `AutoMangator-Windows-x64.exe`. Un unico file portable: scaricalo e avvialo, non serve installare nulla.
- **macOS** (Apple Silicon, macOS 14 o successivo): `AutoMangator-macOS-AppleSilicon.zip`. Estrailo e sposta `AutoMangator.app` in Applicazioni.

## Primo avvio su Mac
L'app non è firmata da Apple (richiederebbe un abbonamento a pagamento), quindi al primo avvio macOS la blocca. Si sblocca una volta sola, in uno di questi due modi:
- **Impostazioni di Sistema → Privacy e sicurezza**, in fondo alla pagina: **Apri comunque**;
- oppure nel Terminale: `xattr -dr com.apple.quarantine /Applications/AutoMangator.app`

Alla prima conversione l'app scarica WebKit, il motore di Safari (una sola volta, circa un centinaio di MB).

## Cosa spuntare
Di solito niente: incolla l'indirizzo e premi **Crea CBZ**. Funziona sia con i siti che mostrano tutto il capitolo
sia con i **reader a una pagina per volta**, che l'app riconosce e sfoglia da sola.
- **Mostra il browser**: per i siti con protezione anti-bot. L'app se ne accorge, te lo dice e spunta la casella:
  basta premere di nuovo il pulsante (e completare la verifica nella finestra, se compare).
- **Sfoglia il reader pagina per pagina**: solo se un reader non viene riconosciuto e arriva soltanto la prima pagina.

Durante il download popup e nuove schede vengono chiusi e la pagina non può essere portata su altri siti.

## Lista capitoli e volumi
- **Trova capitoli**: incolla la pagina del sito che elenca i capitoli, scegli dal/al e gli indirizzi si compilano da soli.
- **Crea singolo volume**: tutti i capitoli in un solo file (CBZ o EPUB con indice per capitolo e numero del volume).
  Se un capitolo non si scarica, l'app chiede se creare comunque il volume con gli altri.

## EPUB per Kobo
Il Kobo non legge i dati contenuti nei `.cbz`. AutoMangator può creare per ogni capitolo un `.kepub.epub` con serie,
numero, autore e gli altri dati: spuntando **Crea EPUB per Kobo** nella finestra principale mentre scarichi, oppure
con **Converti CBZ in EPUB Kobo...** partendo da una cartella di `.cbz` già scaricati. Basta poi copiarli nel Kobo via USB.

## Browser usato
- Windows: Microsoft Edge, già presente nel sistema.
- macOS: WebKit di Playwright, scaricato automaticamente.

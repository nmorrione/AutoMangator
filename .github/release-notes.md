## Download
- **Windows** (10/11, 64 bit): `AutoMangator-Windows-x64.exe`. Un unico file portable: scaricalo e avvialo, non serve installare nulla.
- **macOS** (Apple Silicon, macOS 14 o successivo): `AutoMangator-macOS-AppleSilicon.zip`. Estrailo e sposta `AutoMangator.app` in Applicazioni.

## Primo avvio su Mac
L'app non è firmata da Apple (richiederebbe un abbonamento a pagamento), quindi al primo avvio macOS la blocca. Si sblocca una volta sola, in uno di questi due modi:
- **Impostazioni di Sistema → Privacy e sicurezza**, in fondo alla pagina: **Apri comunque**;
- oppure nel Terminale: `xattr -dr com.apple.quarantine /Applications/AutoMangator.app`

Alla prima conversione l'app scarica WebKit, il motore di Safari (una sola volta, circa un centinaio di MB).

## Browser usato
- Windows: Microsoft Edge, già presente nel sistema.
- macOS: WebKit di Playwright, scaricato automaticamente.

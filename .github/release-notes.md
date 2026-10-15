🇬🇧 English · [🇮🇹 Italiano](https://github.com/nmorrione/Web2Comic/blob/main/README.it.md) · [🇪🇸 Español](https://github.com/nmorrione/Web2Comic/blob/main/README.es.md)

## What's new in 1.8.1
- **Find chapters** now also recognizes sites whose chapter addresses end with a code instead of the number (the number is read from the link text).
- Fixed readers that reload the whole page at every page turn: their pages were found but then skipped.
- The file name uses the chapter title even when the reader ends on a page without one.

## Download
- **Windows** (10/11, 64-bit): `Web2Comic-Windows-x64.exe`. A single portable file: download it and run it, nothing to install.
- **macOS** (Apple Silicon, macOS 14 or later): `Web2Comic-macOS-AppleSilicon.zip`. Extract it and move `Web2Comic.app` to Applications.

## First launch
- **Windows**: if "Windows protected your PC" appears, click **More info → Run anyway** (the app isn't signed with a paid certificate).
- **Mac**: the app isn't signed by Apple, so macOS blocks it the first time. Unlock it once from
  **System Settings → Privacy & Security → Open Anyway**, or in Terminal: `xattr -dr com.apple.quarantine /Applications/Web2Comic.app`.
  On the first conversion the app downloads WebKit, Safari's engine (only once, about a hundred MB).

## What it does
Paste the address of a chapter and Web2Comic creates a `.cbz` comic or an **EPUB** with title, series and author:
- works with pages that show the whole chapter and with **page-by-page readers**, which it detects and pages through on its own;
- **Find chapters** fills in the addresses from the page that lists a series' chapters (with a from/to range);
- **Create a single volume** merges all the chapters into one file, with a table of contents;
- **Download several chapters in parallel** (up to 8 at once) is about twice as fast, and chapters that hit a momentary site error are retried automatically;
- **Optimized for Kobo** creates `.kepub.epub` files, which the Kobo shows full screen;
- pop-ups and new tabs are closed, and the page can't be taken to other sites;
- interface in **English, Italian and Spanish**.

Usually there's nothing to tick. If a site has anti-bot protection the app tells you and ticks **Show the browser** for you:
press the button again and, if a check appears in the window, complete it yourself.

Full guide in the [README](https://github.com/nmorrione/Web2Comic#readme).

# Accessible Arena

Barrierefreiheits-Mod für Magic: The Gathering Arena, der blinden und sehbehinderten Spielern das Spielen mit einem Screenreader ermöglicht. Vollständige Tastaturnavigation, Screenreader-Ansagen für alle Spielzustände und Lokalisierung in 12 Sprachen.

**Status:** Öffentliche Beta. Die Kernfunktionalität ist spielbar. Einige Randfälle und kleinere Fehler bestehen noch. Siehe Bekannte Probleme unten.

**Hinweis:** Derzeit nur Tastatursteuerung. Es gibt keine Maus- oder Touch-Unterstützung. Nur unter Windows 11 mit NVDA getestet. Andere Windows-Versionen und Screenreader (JAWS, Narrator usw.) könnten funktionieren, sind aber ungetestet.

## Funktionen

- Vollständige Tastaturnavigation für alle Bildschirme (Startseite, Shop, Meisterschaft, Deckbauer, Duelle)
- Screenreader-Integration über die Tolk-Bibliothek
- Karteninformationen mit Pfeiltasten vorlesen (Name, Manakosten, Typ, Stärke/Widerstandskraft, Regeltext, Flavourtext, Seltenheit, Künstler)
- Vollständige Duell-Unterstützung: Zonennavigation, Kampf, Zielwahl, Stapel, Browser (Hellsicht, Ausspähen, Mulligan)
- Ansagen zu Anlage- und Kampfbeziehungen (verzaubert von, blockt, Ziel von)
- Barrierefreier Shop mit Kaufoptionen und Zahlungsdialog-Unterstützung
- Bot-Match-Unterstützung für Übungsspiele
- Einstellungsmenü (F2) und Hilfemenü (F1) überall verfügbar
- 12 Sprachen: Englisch, Deutsch, Französisch, Spanisch, Italienisch, Portugiesisch (BR), Japanisch, Koreanisch, Russisch, Polnisch, Chinesisch (vereinfacht), Chinesisch (traditionell)

## Voraussetzungen

- Windows 10 oder höher
- Magic: The Gathering Arena (über den offiziellen Installer oder den Epic Games Store installiert)
- Ein Screenreader (NVDA empfohlen: https://www.nvaccess.org/download/)
- MelonLoader (wird vom Installer automatisch installiert)

## Installation

### Mit dem Installer (empfohlen)

1. Laden Sie `AccessibleArenaInstaller.exe` von der neuesten Version auf GitHub herunter: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Schließen Sie MTG Arena, falls es läuft
3. Führen Sie den Installer aus. Er erkennt Ihre MTGA-Installation, installiert bei Bedarf MelonLoader und stellt den Mod bereit
4. Starten Sie MTG Arena. Sie sollten „Accessible Arena v... gestartet" über Ihren Screenreader hören

### Manuelle Installation

1. Installieren Sie MelonLoader in Ihrem MTGA-Ordner (https://github.com/LavaGang/MelonLoader)
2. Laden Sie `AccessibleArena.dll` von der neuesten Version herunter
3. Kopieren Sie die DLL nach: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Stellen Sie sicher, dass `Tolk.dll` und `nvdaControllerClient64.dll` im MTGA-Stammordner liegen
5. Starten Sie MTG Arena

## Schnellstart

Wenn Sie noch kein Wizards-Konto haben, können Sie eines unter https://myaccounts.wizards.com/ erstellen, anstatt den Registrierungsbildschirm im Spiel zu nutzen.

Nach der Installation starten Sie MTG Arena. Der Mod gibt den aktuellen Bildschirm über Ihren Screenreader aus.

- Drücken Sie jederzeit **F1** für ein navigierbares Hilfemenü mit allen Tastenkombinationen
- Drücken Sie **F2** für das Einstellungsmenü (Sprache, Ausführlichkeit, Tutorial-Nachrichten)
- Drücken Sie **F3**, um den Namen des aktuellen Bildschirms zu hören
- Verwenden Sie **Pfeil hoch/runter** oder **Tab/Umschalt+Tab** zur Menünavigation
- Drücken Sie **Eingabe** oder **Leertaste** zum Aktivieren
- Drücken Sie **Rücktaste** zum Zurückgehen

## Tastenkombinationen

### Menüs

- Pfeil hoch/runter (oder W/S): Elemente navigieren
- Tab/Umschalt+Tab: Elemente navigieren (wie Pfeil hoch/runter)
- Pfeil links/rechts (oder A/D): Karussell- und Schrittregler
- Pos1/Ende: Zum ersten/letzten Element springen
- Bild auf/Bild ab: Vorherige/nächste Seite in der Sammlung
- Eingabe/Leertaste: Aktivieren
- Rücktaste: Zurück

### Duelle - Zonen

- C: Deine Hand
- G / Umschalt+G: Dein Friedhof / Gegnerischer Friedhof
- X / Umschalt+X: Dein Exil / Gegnerisches Exil
- S: Stapel
- B / Umschalt+B: Deine Kreaturen / Gegnerische Kreaturen
- A / Umschalt+A: Deine Länder / Gegnerische Länder
- R / Umschalt+R: Deine Nicht-Kreaturen / Gegnerische Nicht-Kreaturen

### Duelle - Innerhalb von Zonen

- Links/Rechts: Karten navigieren
- Pos1/Ende: Zur ersten/letzten Karte springen
- Pfeil hoch/runter: Kartendetails vorlesen, wenn auf einer Karte fokussiert
- I: Erweiterte Karteninfo (Schlüsselwortbeschreibungen, andere Seiten)
- Umschalt+Hoch/Runter: Schlachtfeldreihen wechseln

### Duelle - Informationen

- T: Aktuelle Runde und Phase
- L: Lebenspunkte
- V: Spieler-Info-Zone (Links/Rechts zum Spielerwechsel, Hoch/Runter für Eigenschaften)
- D / Umschalt+D: Deine Bibliotheksgröße / Gegnerische Bibliotheksgröße
- Umschalt+C: Gegnerische Handkartenanzahl

### Duelle - Aktionen

- Leertaste: Bestätigen (Priorität abgeben, Angreifer/Blocker bestätigen, nächste Phase)
- Rücktaste: Abbrechen / ablehnen
- Tab: Ziele oder hervorgehobene Elemente durchschalten
- Strg+Tab: Nur gegnerische Ziele durchschalten
- Eingabe: Ziel auswählen

### Duelle - Browser (Hellsicht, Ausspähen, Mulligan)

- Tab: Alle Karten navigieren
- C/D: Zur oberen/unteren Zone springen
- Links/Rechts: Innerhalb der Zone navigieren
- Eingabe: Kartenplatzierung umschalten
- Leertaste: Auswahl bestätigen
- Rücktaste: Abbrechen

### Global

- F1: Hilfemenü
- F2: Einstellungsmenü
- F3: Aktuellen Bildschirm ansagen
- Strg+R: Letzte Ansage wiederholen
- Rücktaste: Universell zurück/schließen/abbrechen

## Fehler melden

Wenn Sie einen Fehler finden, öffnen Sie bitte ein Issue auf GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Geben Sie folgende Informationen an:

- Was Sie getan haben, als der Fehler auftrat
- Was Sie erwartet haben
- Was tatsächlich passiert ist
- Ihr Screenreader und dessen Version
- Hängen Sie die MelonLoader-Protokolldatei an: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Bekannte Probleme

- Die Leertaste zum Priorität-Abgeben funktioniert nicht immer zuverlässig (der Mod klickt den Button direkt als Fallback)
- Karten in der Deckliste des Deckbauers zeigen nur Name und Anzahl, keine vollständigen Kartendetails
- Die PlayBlade-Warteschlangenauswahl (Ranked, Offenes Spiel, Brawl) stellt nicht immer den korrekten Spielmodus ein

Die vollständige Liste finden Sie in docs/KNOWN_ISSUES.md.

## Fehlerbehebung

**Keine Sprachausgabe nach dem Starten des Spiels**
- Stellen Sie sicher, dass Ihr Screenreader läuft, bevor Sie MTG Arena starten
- Prüfen Sie, ob `Tolk.dll` und `nvdaControllerClient64.dll` im MTGA-Stammordner liegen (der Installer platziert sie automatisch)
- Prüfen Sie das MelonLoader-Protokoll unter `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` auf Fehler

**Spiel stürzt beim Start ab oder Mod wird nicht geladen**
- Stellen Sie sicher, dass MelonLoader installiert ist.
- Wenn das Spiel kürzlich aktualisiert wurde, müssen MelonLoader oder der Mod möglicherweise neu installiert werden. Führen Sie den Installer erneut aus.
- Prüfen Sie, ob `AccessibleArena.dll` in `C:\Program Files\Wizards of the Coast\MTGA\Mods\` liegt

**Mod funktionierte, aber hörte nach einem Spielupdate auf**
- MTG Arena-Updates können MelonLoader-Dateien überschreiben. Führen Sie den Installer erneut aus, um sowohl MelonLoader als auch den Mod neu zu installieren.
- Wenn das Spiel seine interne Struktur erheblich geändert hat, benötigt der Mod möglicherweise ein Update. Prüfen Sie auf neue Versionen auf GitHub.

**Tastenkombinationen funktionieren nicht**
- Stellen Sie sicher, dass das Spielfenster fokussiert ist (klicken Sie darauf oder verwenden Sie Alt+Tab)
- Drücken Sie F1, um zu prüfen, ob der Mod aktiv ist. Wenn Sie das Hilfemenü hören, läuft der Mod.
- Einige Tastenkombinationen funktionieren nur in bestimmten Kontexten (Duell-Tastenkombinationen nur während eines Duells)

**Falsche Sprache**
- Drücken Sie F2, um das Einstellungsmenü zu öffnen, und verwenden Sie dann Eingabe, um die Sprachen durchzuschalten

## Aus dem Quellcode erstellen

Voraussetzungen: .NET SDK (jede Version, die net472 als Ziel unterstützt)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

Die erstellte DLL befindet sich unter `src/bin/Debug/net472/AccessibleArena.dll`.

Die Spielassembly-Referenzen werden im Ordner `libs/` erwartet. Kopieren Sie diese DLLs aus Ihrer MTGA-Installation (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

MelonLoader-DLLs (`MelonLoader.dll`, `0Harmony.dll`) stammen aus Ihrer MelonLoader-Installation.

## Lizenz

Dieses Projekt ist unter der GNU General Public License v3.0 lizenziert. Siehe die LICENSE-Datei für Details.

## Links

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- NVDA Screenreader (empfohlen): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena

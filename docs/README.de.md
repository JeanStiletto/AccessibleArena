<h1>Accessible Arena</h1>

<h2>Was ist dieser Mod</h2>

Mit diesem Mod kannst du Arena spielen, die beliebteste und einsteigerfreundlichste digitale Umsetzung des Sammelkartenspiels Magic: The Gathering. Der Mod fügt vollständige Screenreader-Unterstützung und Tastaturnavigation für nahezu jeden Teil des Spiels hinzu.

Der Mod unterstützt alle Sprachen, in die das Spiel übersetzt ist. Zusätzlich werden einige Sprachen teilweise abgedeckt, die das Spiel selbst nicht unterstützt: in diesen werden mod-spezifische Ansagen wie Hilfetexte und UI-Hinweise übersetzt, während Karten- und Spieldaten in der Standardsprache des Spiels bleiben.

<h2>Was ist Magic: The Gathering</h2>

Magic ist ein von Wizards of the Coast markenrechtlich geschütztes Sammelkartenspiel, in dem man als Magier gegen andere Magier spielt und durch die Karten repräsentierte Zauber wirkt. Es gibt 5 Farben in Magic, die verschiedene Identitäten von Spielmechanik und Flavour repräsentieren. Wenn du Hearthstone oder Yu-Gi-Oh kennst, wirst du viele Konzepte wiedererkennen, denn Magic ist der Vorfahr all dieser Spiele.
Wenn du mehr über Magic im Allgemeinen lernen möchtest, helfen dir die offizielle Website sowie viele Content Creator weiter.

<h2>Voraussetzungen</h2>

- Windows 10 oder neuer
- Magic: The Gathering Arena (installiert über den offiziellen Wizards-Installer oder Steam)
- Ein Screenreader (nur NVDA und JAWS sind getestet)
- MelonLoader (der Installer kümmert sich automatisch darum)

<h2>Installation</h2>

<h3>Mit dem Installer (empfohlen)</h3>

1. [AccessibleArenaInstaller.exe herunterladen](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) vom neuesten Release auf GitHub
2. Schließe MTG Arena, falls es läuft
3. Starte den Installer. Er erkennt deine MTGA-Installation, installiert bei Bedarf MelonLoader und stellt den Mod bereit
4. Starte MTG Arena. Du solltest "Accessible Arena v... launched" über deinen Screenreader hören

<h3>Manuelle Installation</h3>

1. Installiere [MelonLoader](https://github.com/LavaGang/MelonLoader) in deinen MTGA-Ordner
2. Lade `AccessibleArena.dll` vom neuesten Release herunter
3. Kopiere die DLL in deinen MTGA-Mods-Ordner:
   - WotC-Installation: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Steam-Installation: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Stelle sicher, dass `Tolk.dll` und `nvdaControllerClient64.dll` im MTGA-Hauptverzeichnis liegen
5. Starte MTG Arena

<h2>Deinstallation</h2>

Starte den Installer erneut. Wenn der Mod bereits installiert ist, bietet er eine Deinstallationsoption an. Optional kannst du auch MelonLoader entfernen. Zum manuellen Deinstallieren lösche `AccessibleArena.dll` aus dem `Mods\`-Ordner und entferne `Tolk.dll` sowie `nvdaControllerClient64.dll` aus dem MTGA-Hauptverzeichnis.

<h2>Wenn du von Hearthstone kommst</h2>

Wenn du Hearthstone Access gespielt hast, wirst du aus gutem Grund viele Dinge wiedererkennen, denn nicht nur die Spielprinzipien sind sich ähnlich, sondern ich habe mich auch an vielen Designprinzipien orientiert. Trotzdem sind einige Dinge anders.

Zunächst hast du mehr Zonen zum Navigieren, denn Magic kennt Friedhof, Exil und einige weitere Zonen. Dein Schlachtfeld ist nicht in der Größe begrenzt und hat zusätzliche Sortierreihen, um die Masse an Dingen, die auftauchen können, handhabbarer zu machen.

Dein Mana steigt nicht automatisch, sondern kommt aus verschiedenfarbigen Landkarten, die du aktiv spielen musst. Dementsprechend haben Manakosten farblose und farbige Anteile, die zusammen die vollen Kosten ergeben, die du für eine Karte erfüllen musst.

Du kannst Kreaturen nicht direkt angreifen, nur Gegner und einige sehr spezifische Karten (Planeswalker und Kämpfe) können von Angreifern anvisiert werden. Als Verteidiger musst du entscheiden, ob du einen Angriff blocken willst, um Kreaturen gegeneinander kämpfen zu lassen. Wenn du nicht blockst, trifft der Schaden deinen Spieler-Avatar, aber deine Kreaturen können unberührt bleiben. Außerdem summiert sich Schaden nicht auf Kreaturen, sondern wird am Ende jedes Zuges geheilt, also auch am Ende deines und des gegnerischen Zuges. Um mit gegnerischen Kreaturen zu interagieren, die sich weigern zu kämpfen, musst du bestimmte Karten spielen oder den Lebenspunktestand des Gegners so unter Druck setzen, dass er keine Wahl hat, als wertvolle Kreaturen zu opfern, um zu überleben.

Das Spiel hat sehr klar abgegrenzte Kampfphasen, die bestimmte Aktionen wie Ziehen, Zaubersprüche wirken oder Kämpfen erlauben. Dadurch erlaubt und ermutigt Magic dich, auch im Zug des Gegners Dinge zu tun. Nicht mehr einfach warten, während Sachen passieren. Spiel ein interaktives Deck und zerstöre gegnerische Pläne im Flug.

<h2>Erste Schritte</h2>

Das Spiel fragt dich zuerst nach einigen Daten über dich und dich einen Charakter zu registrieren. Das sollte über die Spielinternen Mittel funktionieren, aber falls nicht, kannst du alternativ die Website des Spiels nutzen, sie ist vollständig barrierefrei.

Das Spiel beginnt mit einem Tutorial, in dem du die Grundlagen von Magic: The Gathering lernst. Der Mod fügt eigene Tutorial-Hinweise für Screenreader-Nutzer parallel zum Standard-Tutorial hinzu. Nach Abschluss des Tutorials bekommst du 5 Starterdecks als Belohnung, eines für jede Farbe.

Von dort hast du mehrere Möglichkeiten, mehr Karten freizuschalten und das Spiel zu lernen:

- **Farbherausforderungen:** Spiel die Farbherausforderung für jede der fünf Magic-Farben durch. Jede Herausforderung lässt dich gegen 4 NPC-Gegner antreten, gefolgt von einem Match gegen einen echten Spieler am Ende.
- **Starterdeck-Events:** Spiele eines von 10 zweifarbigen Decks gegen echte Menschen, die die gleichen Deck-Optionen zur Auswahl haben.
- **Jump In:** Wähle zwei 20-Karten-Pakete aus verschiedenen Farben und Themen, kombiniere sie zu einem Deck und spiele gegen echte Menschen mit ähnlichen Auswahlen. Du bekommst kostenlose Tokens für dieses Event und behältst die Karten, die du gewählt hast.
- **Spark Ladder:** Irgendwann schaltet sich die Spark Ladder frei, wo du deine ersten Ranglistenspiele gegen echte Gegner spielst.

Schau deine Post im Social-Menü an, dort findest du viele Belohnungen und Kartenpakete.

Das Spiel schaltet Modi schrittweise frei, basierend darauf, was und wie viel du spielst. Es gibt dir Hinweise und Aufträge im Fortschritts- und Zielmenü und hebt relevante Modi unter dem Spielen-Menü für dich hervor. Sobald du genug neue-Spieler-Inhalte abgeschlossen hast, werden alle Modi und Events vollständig verfügbar.

Im Kodex des Multiversums kannst du etwas über Spielmodi und Mechaniken lernen. Er erweitert sich mit wachsendem Fortschritt in der Einsteigererfahrung.

Unter Einstellungen-Konto kannst du alle Tutorial-Erfahrungen überspringen und alles per Zwang freischalten, um von Anfang an volle Freiheit zu haben. Das Spielen der neue-Spieler-Events gibt dir allerdings viele Karten und wird für neue Spieler empfohlen. Schalte nur dann alles früh frei, wenn du schon weißt, was du tust. Sonst bieten die Anfängerinhalte jede Menge Spaß und Lernen und führen dich gut durch.

<h2>Tastenkürzel</h2>

Die Navigation folgt überall Standardkonventionen: Pfeiltasten zum Bewegen, Home/End zum Springen zum Anfang/Ende, Enter zum Auswählen, Leertaste zum Bestätigen, Rücktaste zum Zurückgehen oder Abbrechen. Tab/Shift+Tab funktioniert ebenfalls zur Navigation. Bild auf/Bild ab wechselt Seiten.

<h3>Global</h3>

- F1: Hilfemenü (listet alle Tastenkürzel für den aktuellen Bildschirm)
- Strg+F1: Tastenkürzel für den aktuellen Bildschirm ansagen
- F2: Mod-Einstellungen
- F3: Aktuellen Bildschirm ansagen
- F4: Freundesliste (in Menüs) / Duell-Chat (in Duellen)
- F5: Nach Update suchen / Update starten
- Strg+R: Letzte Ansage wiederholen

<h3>Duelle - Zonen</h3>

Deine Zonen: C (Hand), G (Friedhof), X (Exil), S (Stapel), W (Kommandozone)
Gegnerische Zonen: Shift+G, Shift+X, Shift+W
Schlachtfeld: B / Shift+B (Kreaturen), A / Shift+A (Länder), R / Shift+R (Nicht-Kreaturen)
Innerhalb der Zonen: Links/Rechts zum Navigieren, Hoch/Runter zum Lesen der Kartendetails, I für erweiterte Infos
Shift+Hoch/Runter: Zwischen Schlachtfeld-Reihen wechseln

<h3>Duelle - Informationen</h3>

- T: Zug/Phase
- L: Lebenspunkte
- V: Spielerinfo-Zone
- D / Shift+D: Bibliotheksgröße
- Shift+C: Gegnerische Handgröße
- M / Shift+M: Deine / gegnerische Länder-Zusammenfassung
- K: Marken-Info auf fokussierter Karte
- O: Spielprotokoll (letzte Duell-Ansagen)
- E / Shift+E: Dein / gegnerischer Timer

<h3>Duelle - Zielwahl und Aktionen</h3>

- Tab / Strg+Tab: Ziele durchschalten (alle / nur gegnerische)
- Enter: Ziel auswählen
- Leertaste: Priorität abgeben, Angreifer/Blocker bestätigen, Phase weitergehen

<h3>Duelle - Full Control und Phase Stops</h3>

- P: Full Control umschalten (temporär, setzt sich bei Phasenwechsel zurück)
- Shift+P: Gesperrtes Full Control umschalten (permanent)
- Shift+Rücktaste: Pass bis gegnerische Aktion umschalten (weicher Skip)
- Strg+Rücktaste: Zug überspringen umschalten (erzwungener Skip des ganzen Zuges)
- 1-0: Phase Stops umschalten (1=Versorgung, 2=Ziehen, 3=Erste Hauptphase, 4=Kampfbeginn, 5=Angreifer deklarieren, 6=Blocker deklarieren, 7=Kampfschaden, 8=Kampfende, 9=Zweite Hauptphase, 0=Endschritt)

<h3>Duelle - Browser (Hellsicht, Ausspähen, Mulligan)</h3>

- Tab: Alle Karten durchnavigieren
- C/D: Zwischen oberer/unterer Zone springen
- Enter: Kartenplatzierung umschalten

<h2>Fehlerbehebung</h2>

<h3>Keine Sprachausgabe nach dem Start des Spiels</h3>

- Stelle sicher, dass dein Screenreader läuft, bevor du MTG Arena startest
- Prüfe, ob `Tolk.dll` und `nvdaControllerClient64.dll` im MTGA-Hauptverzeichnis liegen (der Installer legt sie automatisch ab)
- Prüfe das MelonLoader-Log in deinem MTGA-Ordner (`MelonLoader\Latest.log`) auf Fehler

<h3>Spiel stürzt beim Start ab oder Mod lädt nicht</h3>

- Stelle sicher, dass MelonLoader installiert ist.
- Wenn das Spiel kürzlich aktualisiert wurde, müssen MelonLoader oder der Mod eventuell neu installiert werden. Starte den Installer erneut.
- Prüfe, ob `AccessibleArena.dll` im `Mods\`-Ordner innerhalb deiner MTGA-Installation liegt

<h3>Mod funktionierte, hörte aber nach einem Spiel-Update auf</h3>

- MTG-Arena-Updates können MelonLoader-Dateien überschreiben. Starte den Installer erneut, um sowohl MelonLoader als auch den Mod neu zu installieren.
- Wenn das Spiel seine interne Struktur erheblich geändert hat, braucht der Mod möglicherweise ein Update. Prüfe auf neue Releases auf GitHub.

<h3>Tastenkürzel funktionieren nicht</h3>

- Stelle sicher, dass das Spielfenster fokussiert ist (klick drauf oder wechsle mit Alt+Tab)
- Drücke F1, um zu prüfen, ob der Mod aktiv ist. Wenn du das Hilfemenü hörst, läuft der Mod.
- Manche Tastenkürzel funktionieren nur in bestimmten Kontexten (Duell-Tastenkürzel nur während eines Duells)

<h3>Falsche Sprache</h3>

- Drücke F2, um das Einstellungsmenü zu öffnen, und nutze dann Enter, um durch die Sprachen zu gehen

<h3>Windows warnt vor dem Installer oder der DLL als unsicher</h3>

Der Installer und die Mod-DLL sind nicht codesigniert. Code-Signing-Zertifikate kosten einige hundert Euro pro Jahr, was für ein kostenloses Barrierefreiheitsprojekt nicht realistisch ist. Dadurch warnt Windows SmartScreen und manche Antivirensoftware beim ersten Ausführen des Installers oder markiert die DLL als "unbekannter Herausgeber".

Um zu überprüfen, ob die heruntergeladene Datei der auf GitHub veröffentlichten entspricht, listet jedes Release eine SHA256-Prüfsumme sowohl für `AccessibleArenaInstaller.exe` als auch für `AccessibleArena.dll` auf. Du kannst den Hash deiner heruntergeladenen Datei berechnen und vergleichen:

- PowerShell: `Get-FileHash <dateiname> -Algorithm SHA256`
- Eingabeaufforderung: `certutil -hashfile <dateiname> SHA256`

Wenn der Hash dem im Release-Text entspricht, ist die Datei echt. Um den Installer trotz SmartScreen-Warnung auszuführen, wähle "Weitere Informationen" und dann "Trotzdem ausführen".

<h2>Fehler melden</h2>

Wenn du einen Fehler findest, kannst du dort posten, wo du den Mod veröffentlicht gefunden hast, oder [ein Issue auf GitHub öffnen](https://github.com/JeanStiletto/AccessibleArena/issues).

Bitte gib folgende Informationen an:

- Was du getan hast, als der Fehler auftrat
- Was du erwartet hast
- Was tatsächlich passiert ist
- Wenn du ein Spiel-Log anhängen möchtest, schließe das Spiel und teile die MelonLoader-Log-Datei aus deinem MTGA-Ordner:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Bekannte Probleme</h2>
Das Spiel sollte nahezu jeden Bildschirm im Spiel abdecken, aber einige Randfälle funktionieren möglicherweise nicht vollständig. PayPal blockiert blinde Nutzer mit einem illegalen Captcha ohne Audio-Alternative, daher brauchst du sehende Hilfe oder andere Zahlungsmethoden, wenn du echtes Geld im Spiel ausgeben willst.
Einige bestimmte Events funktionieren möglicherweise nicht vollständig. Drafting mit echten Spielern hat einen Lobby-Bildschirm, der noch nicht unterstützt wird, aber in Quickdraft wählst du Karten gegen Bots, bevor du gegen menschliche Gegner antrittst, das funktioniert und ist ein empfohlener Modus für jeden, der diese Art Erfahrung mag. Cube-Modus ist unangetastet. Ich weiß nicht mal wirklich, worum es dabei geht, und er kostet viele Ingame-Ressourcen. Ich werde das angehen, wenn ich Zeit habe oder auf Anfrage.
Das Kosmetik-System des Spiels mit Emotes, Haustieren, Kartenstilen und Titeln wird bislang nur teilweise unterstützt.
Der Mod wurde nur unter Windows mit NVDA und JAWS getestet und stützt sich noch auf die unveränderte Tolk-Bibliothek. Ich kann Mac- oder Linux-Kompatibilität hier nicht testen, und plattformübergreifende Bibliotheken wie Prism haben die alten .NET-Versionen, auf die das Spiel bisher angewiesen ist, nicht vollständig unterstützt. Ich werde daher nur auf eine umfassendere Bibliothek wechseln, wenn Leute beim Testen entweder auf anderen Plattformen oder mit asiatischen Screenreadern helfen können, die von unverändertem Tolk nicht vollständig unterstützt werden. Zögere also nicht, mich zu kontaktieren, wenn du möchtest, dass ich daran arbeite.

Die aktuelle Liste bekannter Probleme findest du unter [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Disclaimer</h2>
<h3>Andere Barrierefreiheiten</h3>

Dieser Mod heißt Accessible Arena hauptsächlich, weil es gut klingt. Aber im Moment ist dies nur ein Screenreader-Barrierefreiheits-Mod. Ich bin absolut daran interessiert, weitere Behinderungen mit diesem Mod abzudecken, Sehbeeinträchtigungen, motorische Behinderungen etc. Aber ich habe nur Erfahrung in Screenreader-Barrierefreiheit. Als vollständig blinde Person sind beispielsweise Fragen zu Farbgebung und Schriftarten für mich völlig abstrakt. Also wenn du etwas in dieser Art implementiert haben möchtest, zögere bitte nicht, mich zu kontaktieren, wenn du deine Bedürfnisse klar beschreiben kannst und bereit bist, mir beim Testen der Ergebnisse zu helfen.
Dann mache ich den Namen dieses Mods gerne wahrhaftiger.

<h3>Kontakt zum Unternehmen</h3>

Leider konnte ich keine zuverlässigen Einblicke in das Arena-Team oder informelle Entwickler-Kontakte bekommen. Deshalb habe ich mich entschieden, ihre offiziellen Kommunikationskanäle vorerst zu umgehen. In 3 Monaten Entwicklung und Spielen bin ich nie auf ein Bot-Schutz-System gestoßen, also glaube ich nicht, dass sie uns als Mod-Nutzer erkennen können. Aber ich wollte nicht das Risiko eingehen, als Einzelperson über offizielle Kanäle zu kommunizieren. Also verbreite das Wort über den Mod und lasst uns eine große, wertvolle Community aufbauen. Dann haben wir eine viel bessere Position, falls wir den direkten Kontakt suchen. Versuche einfach nicht, ihnen zu schreiben, ohne dich vorher mit mir abzustimmen. Sende ihnen insbesondere keine Anfragen zu nativer Barrierefreiheit oder zur Integration meines Mods in ihre Codebasis. Beides wird in keinem Fall passieren.

<h3>In-Game-Käufe</h3>

Arena hat einige Echtgeld-Mechaniken, und du kannst eine Ingame-Währung kaufen. Die Zahlungsmethoden sind größtenteils barrierefrei, außer PayPal, weil sie einen Captcha-Schutz in ihr Login eingebaut haben. Du kannst versuchen, den Mod für die Zahlungsmethodenregistrierung zu deinstallieren und um sehende Hilfe zu bitten, aber selbst das ist unzuverlässig aufgrund ihres Barrierefreiheits-Albtraums von Captcha, der zudem von Wizards of the Coast absolut fehlerhaft und schlecht implementiert ist.
Aber andere Zahlungsmethoden funktionieren stabil. Ich und andere haben Ingame-Käufe getestet und es sollte sicher sein, das System zu nutzen. Aber es ist absolut möglich, dass Fehler auftreten oder dass der Mod dich in die Irre führt. Könnte auf die falschen Dinge klicken, dir falsche oder unvollständige Informationen zeigen, die falschen Dinge tun aufgrund interner Änderungen von Arena. Ich könnte es testen, aber ich kann nicht zu 100% garantieren, dass du nicht die falschen Dinge mit deinem echten Geld kaufst. Ich übernehme dafür keine Verantwortung, und weil dies kein offizielles Arena-Produkt ist, wird die Spielefirma das auch nicht tun. Bitte versuche nicht einmal, in diesem Fall Rückerstattungen zu bekommen, die wirst du nicht bekommen.

<h3>KI-Nutzung</h3>

Der Code dieses Mods ist zu 100% mit Hilfe des Claude-Agenten von Anthropic unter Nutzung der Opus-Modelle entstanden: es begann mit 4.5, die meiste Entwicklung geschah auf 4.6, und die letzten Schritte zum Release wurden mit 4.7 gemacht. Und dank meines größten Contributors auch etwas Codex. Ich bin mir der Probleme mit KI-Nutzung bewusst. Aber in einer Zeit, in der alle diese Software für deutlich zwielichtigere Dinge nutzen, während die Spielebranche uns die Barrierefreiheit, die wir wollen, in Qualität und Quantität nicht geben konnte, habe ich mich dennoch entschieden, die Tools zu nutzen.

<h2>Wie du beitragen kannst</h2>

Ich freue mich über Beiträge, und mit [blindndangerous](https://github.com/blindndangerous) ist bereits viel hilfreiche Arbeit einer anderen Person Teil dieses Mods. Ich bin besonders an Verbesserungen und Fixes für Dinge interessiert, die ich nicht testen kann, wie verschiedene Systemkonfigurationen, Korrekturen für Sprachen, die ich nicht spreche etc. Aber nehme auch Feature-Wünsche entgegen. Bevor du an etwas arbeitest, wirf einen Blick auf die bekannten Probleme.

- Allgemeine Beitragsrichtlinien: [CONTRIBUTING.md](../CONTRIBUTING.md)
- Hilfe bei Übersetzungen: [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Danksagungen</h2>

Und jetzt möchte ich einer Menge Leute danken, denn zum Glück war das nicht nur ich und die KI in einer Blackbox, sondern ein ganzes Netzwerk um mich herum, das geholfen, gestärkt, einfach sozial und nett war.
Bitte schreibt mir per DM, wenn ich euch vergessen habe oder ihr unter einem anderen Namen bekannt sein oder nicht erwähnt werden wollt.

Zuerst basiert diese Arbeit sehr stark auf der Arbeit anderer Leute, die die Pionierarbeit geleistet haben, die ich für Accessible Arena nur wiederholen musste.
In Bezug auf Design ist es Hearthstone Access, von dem ich viel übernehmen konnte, nicht nur, weil es jedem bekannt ist, der das Spiel gespielt hat, sondern weil es wirklich gutes UI-Design ist.
In Sachen Modding möchte ich den Mitgliedern von Zax' Modding-Discord danken. Ihr habt nicht nur all die Tools und Abläufe herausgefunden, die ich nur installieren und nutzen musste. Ihr habt mir alles beigebracht, was ich über KI-Modding wissen muss, entweder direkt oder durch öffentliche Diskussionen und das Helfen anderer Neulinge. Außerdem habt ihr mir eine Plattform und Community gegeben, in der ich und mein Projekt existieren können.

Für große Code-Beiträge möchte ich [blindndangerous](https://github.com/blindndangerous) danken, der ebenfalls viel Arbeit an diesem Projekt geleistet hat. Über die Projektlaufzeit habe ich glaube ich über 50 PRs von ihm bekommen, zu allen möglichen Problemen, von nervigem Kleinkram bis hin zu größeren UI-Vorschlägen und Barrierefreiheit für ganze Spielbildschirme.
Weiterer Dank an Ahix, der [Refactoring-Prompts für große KI-basierte Projekte](https://github.com/ahicks92/llm-mod-refactoring-prompts) erstellt hat, die ich auf meine eigenen Refactorings angewendet habe, um Codequalität und Wartbarkeit sicherzustellen.

Für das Testen der Betas, Feedback und Ideen möchte ich danken:
- Alfi
- Plüschyoda
- Firefly92
- Berenion
- [blindndangerous](https://github.com/blindndangerous)
- Toni Barth
- Chaosbringer216
- ABlindFellow
- SightlessKombat
- hamada
- Zack
- glaroc
- zersiax
- kairos4901
- [patricus3](https://github.com/patricus3)

Für sehendes Testen, um visuelle Abläufe zu verstehen und Dinge zu bestätigen, möchte ich danken:
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Verwendete Tools</h3>

- Claude mit allen enthaltenen Modellen
- MelonLoader
- Harmony für IL-Patching
- Tolk für Screenreader-Kommunikation
- ILSpy für das Dekompilieren des Spielcodes

<h2>Unterstütze deinen Modder</h2>

Diesen Mod zu erstellen hat mir nicht nur viel Spaß und Empowerment gebracht, sondern mich auch wirklich viel Zeit und echtes Geld für Claude-Abonnements gekostet. Die werde ich weiterhin nutzen, um an weiteren Verbesserungen zu arbeiten und die Wartung des Projekts in den nächsten Jahren aufrechtzuerhalten.
Wenn du also bereit und in der Lage bist, eine einmalige oder sogar monatliche Spende zu leisten, kannst du hier vorbeischauen.
Ich würde diese Anerkennung meiner Arbeit sehr schätzen, und sie gibt mir eine stabile Basis, um weiter an Arena und hoffentlich an anderen großen Projekten in der Zukunft zu arbeiten.

[Ko-fi: ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Lizenz</h2>

Dieses Projekt ist unter der GNU General Public License v3.0 lizenziert. Details in der LICENSE-Datei.

<h2>Links</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Andere Sprachen</h2>

[English](../README.md) | [Español](README.es.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)

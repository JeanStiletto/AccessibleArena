# Accessible Arena

Mod di accessibilità per Magic: The Gathering Arena che permette ai giocatori ciechi e ipovedenti di giocare utilizzando uno screen reader. Navigazione completa da tastiera, annunci dello screen reader per tutti gli stati di gioco e localizzazione in 12 lingue.

**Stato:** Beta pubblica. Il gameplay principale è funzionale. Rimangono alcuni casi particolari e bug minori. Vedi Problemi noti di seguito.

**Nota:** Attualmente solo tastiera. Non c'è supporto per mouse o touch. Testato solo su Windows 11 con NVDA. Altre versioni di Windows e screen reader (JAWS, Narrator, ecc.) potrebbero funzionare ma non sono testati.

## Funzionalità

- Navigazione completa da tastiera per tutte le schermate (home, negozio, maestria, costruttore di mazzi, duelli)
- Integrazione con screen reader tramite la libreria Tolk
- Lettura delle informazioni delle carte con i tasti freccia (nome, costo di mana, tipo, forza/costituzione, testo delle regole, testo di ambientazione, rarità, artista)
- Supporto completo per i duelli: navigazione per zone, combattimento, bersagliamento, pila, browser (scrutare, sorvegliare, mulligan)
- Annunci delle relazioni di collegamento e combattimento (incantato da, blocca, bersaglio di)
- Negozio accessibile con opzioni di acquisto e supporto per le finestre di pagamento
- Supporto per partite contro bot per esercitarsi
- Menu impostazioni (F2) e menu aiuto (F1) disponibili ovunque
- 12 lingue: inglese, tedesco, francese, spagnolo, italiano, portoghese (BR), giapponese, coreano, russo, polacco, cinese semplificato, cinese tradizionale

## Requisiti

- Windows 10 o successivo
- Magic: The Gathering Arena (installato tramite l'installer ufficiale o l'Epic Games Store)
- Uno screen reader (NVDA consigliato: https://www.nvaccess.org/download/)
- MelonLoader (l'installer lo gestisce automaticamente)

## Installazione

### Con l'installer (consigliato)

1. Scarica `AccessibleArenaInstaller.exe` dall'ultima versione su GitHub: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Chiudi MTG Arena se è in esecuzione
3. Esegui l'installer. Rileverà la tua installazione di MTGA, installerà MelonLoader se necessario e distribuirà la mod
4. Avvia MTG Arena. Dovresti sentire "Accessible Arena v... avviato" tramite il tuo screen reader

### Installazione manuale

1. Installa MelonLoader nella tua cartella MTGA (https://github.com/LavaGang/MelonLoader)
2. Scarica `AccessibleArena.dll` dall'ultima versione
3. Copia la DLL in: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Assicurati che `Tolk.dll` e `nvdaControllerClient64.dll` siano nella cartella principale di MTGA
5. Avvia MTG Arena

## Avvio rapido

Se non hai ancora un account Wizards, puoi crearne uno su https://myaccounts.wizards.com/ invece di usare la schermata di registrazione nel gioco.

Dopo l'installazione, avvia MTG Arena. La mod annuncia la schermata corrente tramite il tuo screen reader.

- Premi **F1** in qualsiasi momento per un menu di aiuto navigabile con tutte le scorciatoie da tastiera
- Premi **F2** per il menu impostazioni (lingua, verbosità, messaggi del tutorial)
- Premi **F3** per sentire il nome della schermata corrente
- Usa **Freccia su/giù** o **Tab/Maiusc+Tab** per navigare nei menu
- Premi **Invio** o **Spazio** per attivare gli elementi
- Premi **Backspace** per tornare indietro

## Scorciatoie da tastiera

### Menu

- Freccia su/giù (o W/S): Navigare tra gli elementi
- Tab/Maiusc+Tab: Navigare tra gli elementi (come Freccia su/giù)
- Freccia sinistra/destra (o A/D): Controlli carosello e stepper
- Home/Fine: Vai al primo/ultimo elemento
- Pag su/Pag giù: Pagina precedente/successiva nella collezione
- Invio/Spazio: Attiva
- Backspace: Indietro

### Duelli - Zone

- C: La tua mano
- G / Maiusc+G: Il tuo cimitero / Cimitero avversario
- X / Maiusc+X: Il tuo esilio / Esilio avversario
- S: Pila
- B / Maiusc+B: Le tue creature / Creature avversarie
- A / Maiusc+A: Le tue terre / Terre avversarie
- R / Maiusc+R: I tuoi non-creature / Non-creature avversari

### Duelli - Dentro le zone

- Sinistra/Destra: Navigare tra le carte
- Home/Fine: Vai alla prima/ultima carta
- Freccia su/giù: Leggere i dettagli della carta quando è selezionata
- I: Info estesa della carta (descrizioni delle parole chiave, altre facce)
- Maiusc+Su/Giù: Cambiare riga del campo di battaglia

### Duelli - Informazioni

- T: Turno e fase correnti
- L: Totali punti vita
- V: Zona info giocatore (Sinistra/Destra per cambiare giocatore, Su/Giù per le proprietà)
- D / Maiusc+D: Numero carte nella tua libreria / Libreria avversaria
- Maiusc+C: Numero carte in mano avversaria

### Duelli - Azioni

- Spazio: Conferma (passa priorità, conferma attaccanti/bloccanti, fase successiva)
- Backspace: Annulla / rifiuta
- Tab: Scorrere bersagli o elementi evidenziati
- Ctrl+Tab: Scorrere solo bersagli avversari
- Invio: Seleziona bersaglio

### Duelli - Browser (Scrutare, Sorvegliare, Mulligan)

- Tab: Navigare tra tutte le carte
- C/D: Vai alla zona superiore/inferiore
- Sinistra/Destra: Navigare nella zona
- Invio: Alternare il posizionamento della carta
- Spazio: Conferma selezione
- Backspace: Annulla

### Globale

- F1: Menu aiuto
- F2: Menu impostazioni
- F3: Annuncia schermata corrente
- Ctrl+R: Ripeti ultimo annuncio
- Backspace: Indietro/chiudi/annulla universale

## Segnalare bug

Se trovi un bug, apri un issue su GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Includi le seguenti informazioni:

- Cosa stavi facendo quando si è verificato il bug
- Cosa ti aspettavi che succedesse
- Cosa è successo realmente
- Il tuo screen reader e la sua versione
- Allega il file di log di MelonLoader: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Problemi noti

- Il tasto Spazio per passare la priorità non è sempre affidabile (la mod clicca direttamente il pulsante come fallback)
- Le carte nella lista del mazzo nel costruttore mostrano solo nome e quantità, non i dettagli completi
- La selezione del tipo di coda PlayBlade (Classificata, Gioco Aperto, Brawl) non sempre imposta la modalità di gioco corretta

Per la lista completa, vedi docs/KNOWN_ISSUES.md.

## Risoluzione dei problemi

**Nessuna uscita vocale dopo l'avvio del gioco**
- Assicurati che il tuo screen reader sia in esecuzione prima di avviare MTG Arena
- Verifica che `Tolk.dll` e `nvdaControllerClient64.dll` siano nella cartella principale di MTGA (l'installer li posiziona automaticamente)
- Controlla il log di MelonLoader in `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` per errori

**Il gioco si blocca all'avvio o la mod non si carica**
- Assicurati che MelonLoader sia installato.
- Se il gioco è stato aggiornato di recente, potrebbe essere necessario reinstallare MelonLoader o la mod. Esegui di nuovo l'installer.
- Verifica che `AccessibleArena.dll` sia in `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**La mod funzionava ma ha smesso dopo un aggiornamento del gioco**
- Gli aggiornamenti di MTG Arena possono sovrascrivere i file di MelonLoader. Esegui di nuovo l'installer per reinstallare sia MelonLoader che la mod.
- Se il gioco ha cambiato significativamente la sua struttura interna, la mod potrebbe necessitare di un aggiornamento. Controlla le nuove versioni su GitHub.

**Le scorciatoie da tastiera non funzionano**
- Assicurati che la finestra del gioco sia in primo piano (cliccaci sopra o usa Alt+Tab)
- Premi F1 per verificare se la mod è attiva. Se senti il menu di aiuto, la mod è in esecuzione.
- Alcune scorciatoie funzionano solo in contesti specifici (le scorciatoie dei duelli solo durante un duello)

**Lingua sbagliata**
- Premi F2 per aprire il menu impostazioni, poi usa Invio per scorrere le lingue

## Compilare dal codice sorgente

Requisiti: SDK .NET (qualsiasi versione che supporti il target net472)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

La DLL compilata sarà in `src/bin/Debug/net472/AccessibleArena.dll`.

I riferimenti agli assembly del gioco sono attesi nella cartella `libs/`. Copia queste DLL dalla tua installazione di MTGA (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

Le DLL di MelonLoader (`MelonLoader.dll`, `0Harmony.dll`) provengono dalla tua installazione di MelonLoader.

## Licenza

Questo progetto è sotto licenza GNU General Public License v3.0. Vedi il file LICENSE per i dettagli.

## Link

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- Screen reader NVDA (consigliato): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena

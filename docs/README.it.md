<h1>Accessible Arena</h1>

<h2>Cos'è questo mod</h2>

Questo mod ti permette di giocare ad Arena, la rappresentazione digitale più popolare e adatta ai principianti del gioco di carte collezionabili Magic: The Gathering. Aggiunge il supporto completo per lettori di schermo e la navigazione da tastiera a quasi ogni aspetto del gioco.

Il mod supporta tutte le lingue in cui il gioco è tradotto. Inoltre, alcune lingue che il gioco stesso non supporta sono parzialmente coperte: in queste, gli annunci specifici del mod come i testi di aiuto e i suggerimenti dell'interfaccia sono tradotti, mentre i dati delle carte e del gioco rimangono nella lingua predefinita del gioco.

<h2>Cos'è Magic: The Gathering</h2>

Magic è un gioco di carte collezionabili registrato da Wizards of the Coast che ti permette di giocare nei panni di un mago contro altri maghi, lanciando incantesimi rappresentati dalle carte. In Magic esistono 5 colori che rappresentano diverse identità di gameplay e di ambientazione. Se conosci Hearthstone o Yu-Gi-Oh riconoscerai molti concetti perché Magic è l'antenato di tutti quei giochi.
Se vuoi saperne di più su Magic in generale, il sito ufficiale del gioco e molti creatori di contenuti ti saranno di aiuto.

<h2>Requisiti</h2>

- Windows 10 o successivo
- Magic: The Gathering Arena (installato tramite l'installer ufficiale di Wizards o Steam)
- Un lettore di schermo (solo NVDA e JAWS sono testati)
- MelonLoader (l'installer lo gestisce automaticamente)

<h2>Installazione</h2>

<h3>Con l'installer (consigliato)</h3>

1. [Scarica AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) dall'ultima release su GitHub
2. Chiudi MTG Arena se è in esecuzione
3. Esegui l'installer. Rileverà la tua installazione di MTGA, installerà MelonLoader se necessario e distribuirà il mod
4. Avvia MTG Arena. Dovresti sentire "Accessible Arena v... launched" attraverso il tuo lettore di schermo

<h3>Installazione manuale</h3>

1. Installa [MelonLoader](https://github.com/LavaGang/MelonLoader) nella tua cartella MTGA
2. Scarica `AccessibleArena.dll` dall'ultima release
3. Copia la DLL nella tua cartella Mods di MTGA:
   - Installazione WotC: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Installazione Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Assicurati che `Tolk.dll` e `nvdaControllerClient64.dll` siano nella cartella radice di MTGA
5. Avvia MTG Arena

<h2>Disinstallazione</h2>

Esegui di nuovo l'installer. Se il mod è già installato, offrirà un'opzione di disinstallazione. Puoi anche rimuovere MelonLoader se vuoi. Per disinstallare manualmente, elimina `AccessibleArena.dll` dalla cartella `Mods\` e rimuovi `Tolk.dll` e `nvdaControllerClient64.dll` dalla cartella radice di MTGA.

<h2>Se vieni da Hearthstone</h2>

Se hai giocato a Hearthstone Access riconoscerai molte cose per buoni motivi, perché non solo i principi di gioco sono simili, ma ho seguito molti principi di design. Tuttavia alcune cose sono diverse.

Prima di tutto hai più zone da navigare, perché Magic conosce cimitero, esilio e alcune zone extra. Il tuo campo di battaglia non ha una dimensione limitata e ha righe di ordinamento aggiuntive per rendere più gestibile la massa di cose che possono comparire.

Il tuo mana non sale automaticamente ma proviene da carte terra di diversi colori che devi giocare attivamente. Di conseguenza i costi di mana hanno parti incolori e colorate che sommate danno i requisiti di costo completi di una carta che devi soddisfare.

Non puoi attaccare le creature direttamente, solo gli avversari e alcune carte molto specifiche (planeswalker e battaglie) possono essere bersaglio degli attaccanti. In difesa devi decidere se vuoi bloccare un attacco per far combattere le creature. Se non blocchi, il danno colpirà il tuo avatar giocatore ma le tue creature possono restare intatte. Inoltre il danno non si accumula sulle creature ma viene guarito alla fine di ogni turno, quindi sia alla fine del tuo turno che di quello dell'avversario. Per interagire con le creature avversarie che si rifiutano di combattere con te, devi giocare carte specifiche o fare pressione sui punti vita dell'avversario al punto da costringerlo a sacrificare creature preziose per sopravvivere.

Il gioco ha fasi di battaglia molto distinte che permettono azioni specifiche come pescare, lanciare incantesimi o combattere. Di conseguenza Magic permette e favorisce azioni durante il turno dell'avversario. Basta stare seduto e aspettare mentre succedono le cose. Gioca un mazzo interattivo e distruggi al volo i piani del nemico.

<h2>Primi passi</h2>

Il gioco ti chiede prima di fornire alcuni dati su di te e di registrare un personaggio. Dovrebbe funzionare tramite i meccanismi interni del gioco, ma se non funziona puoi in alternativa usare il sito del gioco per farlo, è completamente accessibile.

Il gioco inizia con un tutorial in cui impari le basi di Magic: The Gathering. Il mod aggiunge suggerimenti tutorial personalizzati per gli utenti di lettori di schermo accanto al tutorial standard. Dopo aver completato il tutorial, vieni ricompensato con 5 mazzi iniziali, uno per ogni colore.

Da lì hai diverse opzioni per sbloccare più carte e imparare il gioco:

- **Sfide dei colori:** Gioca la sfida per ciascuno dei cinque colori di Magic. Ogni sfida ti vede affrontare 4 avversari PNG, seguiti da una partita contro un giocatore reale alla fine.
- **Eventi mazzo iniziale:** Gioca uno dei 10 mazzi bicolore contro veri giocatori che hanno le stesse scelte di mazzo disponibili.
- **Jump In:** Scegli due pacchetti da 20 carte di colori e temi diversi, combinali in un mazzo e gioca contro veri giocatori con scelte simili. Ricevi gettoni gratuiti per questo evento e mantieni le carte scelte.
- **Spark Ladder:** A un certo punto si sblocca la Spark Ladder, dove giochi le tue prime partite classificate contro veri avversari.

Controlla la posta nel menu social, contiene molte ricompense e buste di carte.

Il gioco sblocca le modalità gradualmente in base a cosa e quanto giochi. Ti dà suggerimenti e missioni nel menu progressi e obiettivi, ed evidenzia le modalità rilevanti per te nel menu gioca. Una volta completato abbastanza contenuto per nuovi giocatori, tutte le diverse modalità ed eventi diventano pienamente disponibili.

Nel Codex del Multiverso puoi imparare le modalità di gioco e le meccaniche. Si estende con l'avanzamento nell'esperienza NPE.

Sotto impostazioni account puoi saltare tutte le esperienze tutorial e sbloccare forzatamente tutto per avere piena libertà fin dall'inizio. Tuttavia, giocare gli eventi per nuovi giocatori ti dà molte carte ed è consigliato per i nuovi giocatori. Sblocca tutto in anticipo solo se sai già cosa stai facendo. Altrimenti il contenuto per principianti offre molto divertimento e apprendimento guidandoti bene.

<h2>Scorciatoie da tastiera</h2>

La navigazione segue convenzioni standard ovunque: frecce per muoversi, Home/Fine per saltare al primo/ultimo, Invio per selezionare, Spazio per confermare, Backspace per tornare indietro o annullare. Anche Tab/Shift+Tab funzionano per la navigazione. PagSu/PagGiù cambia pagina.

<h3>Globale</h3>

- F1: Menu aiuto (elenca tutte le scorciatoie per la schermata corrente)
- Ctrl+F1: Annuncia le scorciatoie per la schermata corrente
- F2: Impostazioni del mod
- F3: Annuncia la schermata corrente
- F4: Pannello amici (dai menu) / Chat del duello (durante i duelli)
- F5: Verifica / avvia l'aggiornamento
- Ctrl+R: Ripeti l'ultimo annuncio

<h3>Duelli - Zone</h3>

Le tue zone: C (Mano), G (Cimitero), X (Esilio), S (Pila), W (Zona di comando)
Zone dell'avversario: Shift+G, Shift+X, Shift+W
Campo di battaglia: B / Shift+B (Creature), A / Shift+A (Terre), R / Shift+R (Non creature)
All'interno delle zone: Sinistra/Destra per navigare, Su/Giù per leggere i dettagli della carta, I per informazioni estese
Shift+Su/Giù: Cambia riga nel campo di battaglia

<h3>Duelli - Informazioni</h3>

- T: Turno/Fase
- L: Punti vita
- V: Zona info giocatore
- D / Shift+D: Conteggi grimorio
- Shift+C: Carte in mano dell'avversario
- M / Shift+M: Riepilogo terre tue / dell'avversario
- K: Info segnalini sulla carta messa a fuoco
- O: Registro di partita (annunci recenti del duello)
- E / Shift+E: Timer tuo / dell'avversario

<h3>Duelli - Puntamento e azioni</h3>

- Tab / Ctrl+Tab: Cicla bersagli (tutti / solo avversario)
- Invio: Seleziona bersaglio
- Spazio: Passa la priorità, conferma attaccanti/bloccanti, avanza fase

<h3>Duelli - Full control e stop di fase</h3>

- P: Attiva full control (temporaneo, si reimposta al cambio di fase)
- Shift+P: Attiva full control bloccato (permanente)
- Shift+Backspace: Attiva passa fino ad azione dell'avversario (skip morbido)
- Ctrl+Backspace: Attiva salta turno (forza skip dell'intero turno)
- 1-0: Attiva stop di fase (1=Mantenimento, 2=Acquisizione, 3=Prima fase principale, 4=Inizio combattimento, 5=Dichiara attaccanti, 6=Dichiara bloccanti, 7=Danno da combattimento, 8=Fine combattimento, 9=Seconda fase principale, 0=Sotto-fase finale)

<h3>Duelli - Browser (Scry, Sorveglia, Mulligan)</h3>

- Tab: Naviga tutte le carte
- C/D: Salta tra zone alto/basso
- Invio: Alterna collocazione carta

<h2>Risoluzione dei problemi</h2>

<h3>Nessuna voce dopo l'avvio del gioco</h3>

- Assicurati che il tuo lettore di schermo sia in esecuzione prima di avviare MTG Arena
- Controlla che `Tolk.dll` e `nvdaControllerClient64.dll` siano nella cartella radice di MTGA (l'installer li posiziona automaticamente)
- Controlla il log di MelonLoader nella tua cartella MTGA (`MelonLoader\Latest.log`) per errori

<h3>Il gioco va in crash all'avvio o il mod non si carica</h3>

- Assicurati che MelonLoader sia installato.
- Se il gioco è stato aggiornato di recente, potrebbe essere necessario reinstallare MelonLoader o il mod. Esegui di nuovo l'installer.
- Controlla che `AccessibleArena.dll` sia nella cartella `Mods\` all'interno della tua installazione di MTGA

<h3>Il mod funzionava ma si è fermato dopo un aggiornamento del gioco</h3>

- Gli aggiornamenti di MTG Arena possono sovrascrivere i file di MelonLoader. Esegui di nuovo l'installer per reinstallare sia MelonLoader che il mod.
- Se il gioco ha cambiato significativamente la sua struttura interna, il mod potrebbe aver bisogno di un aggiornamento. Controlla le nuove release su GitHub.

<h3>Le scorciatoie da tastiera non funzionano</h3>

- Assicurati che la finestra del gioco sia a fuoco (fai clic su di essa o Alt+Tab per attivarla)
- Premi F1 per verificare se il mod è attivo. Se senti il menu di aiuto, il mod è in esecuzione.
- Alcune scorciatoie funzionano solo in contesti specifici (quelle dei duelli funzionano solo durante un duello)

<h3>Lingua sbagliata</h3>

- Premi F2 per aprire il menu impostazioni, poi usa Invio per scorrere tra le lingue

<h3>Windows avvisa che l'installer o la DLL non sono sicuri</h3>

L'installer e la DLL del mod non sono firmati digitalmente. I certificati di firma codice costano qualche centinaio di euro all'anno, cosa non realistica per un progetto di accessibilità gratuito. Di conseguenza, Windows SmartScreen e alcuni antivirus ti avviseranno quando esegui l'installer per la prima volta o segnaleranno la DLL come "editore sconosciuto".

Per verificare che il file scaricato corrisponda a quello pubblicato su GitHub, ogni release elenca un checksum SHA256 sia per `AccessibleArenaInstaller.exe` che per `AccessibleArena.dll`. Puoi calcolare l'hash del file scaricato e confrontarlo:

- PowerShell: `Get-FileHash <nomefile> -Algorithm SHA256`
- Prompt dei comandi: `certutil -hashfile <nomefile> SHA256`

Se l'hash corrisponde a quello nelle note di rilascio, il file è autentico. Per eseguire l'installer oltre l'avviso SmartScreen, scegli "Ulteriori informazioni" e poi "Esegui comunque".

<h2>Segnalare bug</h2>

Se trovi un bug, puoi pubblicare nel luogo in cui hai trovato il mod, o [aprire una issue su GitHub](https://github.com/JeanStiletto/AccessibleArena/issues).

Includi le seguenti informazioni:

- Cosa stavi facendo quando il bug si è verificato
- Cosa ti aspettavi che accadesse
- Cosa è realmente successo
- Se vuoi allegare un log di gioco, chiudi il gioco e condividi il file log di MelonLoader dalla tua cartella MTGA:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Problemi noti</h2>
Il gioco dovrebbe coprire quasi ogni schermata del gioco, ma ci potrebbero essere alcuni casi limite non completamente funzionanti. PayPal blocca gli utenti ciechi con un captcha illegale non audio, quindi devi usare l'aiuto di una persona vedente o altri metodi di pagamento se vuoi spendere denaro reale nel gioco.
Alcuni eventi specifici potrebbero non essere completamente funzionanti. Il draft con giocatori reali ha una schermata di lobby non ancora supportata, ma in quickdraft scegli le carte contro i bot prima di affrontare avversari umani, questa modalità è funzionale e consigliata a chiunque ami questo tipo di esperienza. La modalità Cube non è stata toccata. Non so nemmeno di cosa si tratti davvero e costa molte risorse di gioco. Quindi ci lavorerò se avrò tempo o su richiesta.
Il sistema cosmetico del gioco con Emote, Animali, stili carta e titoli è per ora supportato solo parzialmente.
Il mod è testato solo su Windows con NVDA e JAWS e si affida tuttora alla libreria Tolk non modificata. Non posso testare qui la compatibilità con Mac o Linux, e librerie multipiattaforma come Prism non supportano pienamente le vecchie versioni di .NET da cui il gioco dipende a questo punto. Perciò passerò a una libreria più ampia solo se qualcuno può aiutare a testare sia altre piattaforme sia lettori di schermo asiatici non pienamente supportati da Tolk non modificato. Non esitare quindi a contattarmi se vuoi che ci lavori.

Per l'elenco corrente dei problemi noti, vedi [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Avvertenze</h2>
<h3>Altre accessibilità</h3>

Questo mod si chiama Accessible Arena soprattutto perché suona bene. Ma al momento è solo un mod di accessibilità per lettori di schermo. Sono assolutamente interessato a coprire più disabilità con questo mod, disabilità visive, motorie ecc. Ma ho esperienza solo nell'accessibilità per lettori di schermo. Come persona totalmente cieca, ad esempio, le questioni di colore e font sono del tutto astratte per me. Quindi se vuoi qualcosa del genere implementato, non esitare a contattarmi se puoi descrivere chiaramente le tue esigenze e sei disposto ad aiutarmi a testare i risultati.
Allora sarò felice di dare al nome di questo mod più verità.

<h3>Contatto con l'azienda</h3>

Purtroppo non sono riuscito ad avere informazioni affidabili sul team di Arena o contatti informali con gli sviluppatori. Per il momento ho quindi deciso di non utilizzare i loro canali ufficiali di comunicazione. In 3 mesi di sviluppo e gioco non ho mai incontrato alcun sistema di protezione anti-bot, quindi non credo possano rilevarci come utenti di mod. Ma non volevo correre il rischio di comunicare sui canali ufficiali come singola persona. Spargete quindi la voce sul mod e costruiamo una comunità grande e di valore. Avremo allora una posizione molto migliore se decideremo di contattarli direttamente. Solo non provate a scrivere loro senza parlarne prima con me. In particolare, non inviate loro richieste di accessibilità nativa o di integrazione del mio mod nella loro codebase. Nessuna delle due cose avverrà in ogni caso.

<h3>Acquisti nel gioco</h3>

Arena ha alcune meccaniche di denaro reale e puoi acquistare una valuta in-game. Questi metodi di pagamento sono per lo più accessibili tranne PayPal perché hanno incluso una protezione captcha al login. Puoi provare a disinstallare il mod per registrare il metodo di pagamento e chiedere aiuto a una persona vedente, ma anche questo non è affidabile a causa del loro incubo di accessibilità del captcha, reso ancora più rotto e mal implementato da Wizards of the Coast.
Ma altri metodi di pagamento funzionano stabilmente. Io e altri abbiamo testato l'acquisto in-game di cose e l'uso del sistema dovrebbe essere sicuro. Ma è assolutamente possibile che si verifichino bug o addirittura che il mod ti fuorvii. Potrebbe cliccare su cose sbagliate, mostrarti informazioni errate o incomplete, fare le cose sbagliate a causa di cambi interni di Arena. Potrei testare, ma non posso garantire al 100% che non potresti comprare le cose sbagliate con il tuo denaro reale. Non mi assumerò responsabilità per questo e, dato che non è un prodotto ufficiale di Arena, non lo farà nemmeno l'azienda del gioco. Per favore, in questo caso non provare nemmeno a ottenere rimborsi, non te li daranno.

<h3>Uso dell'IA</h3>

Il codice di questo mod è stato creato al 100% con l'aiuto dell'agente Claude di Anthropic usando i modelli Opus: è iniziato con il 4.5, la maggior parte dello sviluppo è avvenuta sul 4.6, e gli ultimi passi verso il rilascio sono stati fatti su 4.7. E grazie al mio più grande collaboratore, anche un po' di Codex. Sono consapevole dei problemi dell'uso dell'IA. Ma in un'epoca in cui tutti usano questi software per fare molte cose assai più losche mentre l'industria dei videogiochi non ci ha dato l'accessibilità che vogliamo in termini di qualità o quantità, ho comunque deciso di usare gli strumenti.

<h2>Come contribuire</h2>

Sono felice di accettare contributi e con [blindndangerous](https://github.com/blindndangerous) già molto lavoro utile di un'altra persona fa parte di questo mod. Mi interessano in particolare miglioramenti e correzioni per cose che non posso testare, come configurazioni di sistema diverse, correggere lingue che non parlo ecc. Ma accetto anche richieste di funzionalità. Prima di lavorare su qualcosa, controlla i problemi noti.

- Per le linee guida generali di contribuzione, vedi [CONTRIBUTING.md](../CONTRIBUTING.md)
- Per aiutare nelle traduzioni, vedi [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Ringraziamenti</h2>

E ora voglio ringraziare un sacco di persone, perché fortunatamente questo non è stato solo io e l'IA in una scatola nera ma un'intera rete attorno a me, che aiutava, dava forza, era semplicemente socievole e gentile.
Scrivimi in DM se ti ho dimenticato o se vuoi essere conosciuto con un nome diverso o non essere menzionato.

Prima di tutto, questo lavoro si basa molto sul lavoro di altre persone che hanno fatto il lavoro pionieristico che io ho dovuto solo rifare per Accessible Arena.
In termini di design è Hearthstone Access da cui ho potuto ereditare molto non solo perché è ben noto a tutti quelli che hanno giocato al gioco, ma perché è davvero un buon design di interfaccia.
Per quanto riguarda il modding voglio ringraziare i membri del Discord di modding di Zax. Non solo avete capito tutta la materia, tutti gli strumenti e le procedure che io ho solo dovuto installare e usare. Mi avete insegnato tutto quello che dovevo sapere sul modding con l'IA, sia direttamente sia discutendone in pubblico o aiutando altri principianti. Inoltre mi avete dato una piattaforma e una comunità in cui io e il mio progetto possiamo esistere.

Per enormi contributi al codice voglio ringraziare [blindndangerous](https://github.com/blindndangerous) che ha fatto molto lavoro anche su questo progetto. Nell'arco di vita del progetto penso di aver ricevuto circa 50 PR e più da lui riguardanti ogni tipo di problema, dalle piccole cose fastidiose da sistemare fino a suggerimenti di interfaccia più ampi e l'accessibilità di intere schermate del gioco.
Inoltre grazie ad Ahix che ha creato [prompt di refactoring per grandi progetti creati con l'IA](https://github.com/ahicks92/llm-mod-refactoring-prompts) che ho eseguito oltre i miei refactoring per garantire la qualità del codice e la manutenibilità.

Per i test delle beta, i feedback e le idee voglio ringraziare:
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

Per i test con persone vedenti per capire i flussi visivi e confermare alcune cose voglio ringraziare:
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Strumenti utilizzati</h3>

- Claude con tutti i modelli inclusi
- MelonLoader
- Harmony per il patching IL
- Tolk per la comunicazione con i lettori di schermo
- ILSpy per decompilare il codice del gioco

<h2>Supporta il tuo modder</h2>

Creare questo mod non è stato solo molto divertente e responsabilizzante per me ma mi è costato davvero molto tempo e denaro reale per gli abbonamenti Claude. Li manterrò per lavorare a ulteriori miglioramenti e tenere in manutenzione il progetto nei prossimi anni.
Quindi se sei disposto e in grado di permetterti una donazione una tantum o anche mensile, puoi dare un'occhiata qui.
Apprezzerei davvero questo riconoscimento del mio lavoro e mi dà una base stabile per continuare a lavorare su Arena e, si spera, su altri grandi progetti in futuro.

[Ko-fi: ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Licenza</h2>

Questo progetto è rilasciato sotto la licenza GNU General Public License v3.0. Vedi il file LICENSE per i dettagli.

<h2>Link</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Altre lingue</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Polski](README.pl.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)

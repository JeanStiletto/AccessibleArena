<h1>Accessible Arena</h1>

<h2>Czym jest ten mod</h2>

Ten mod pozwala grać w Arenę, najpopularniejszą i najbardziej przyjazną dla początkujących cyfrową wersję kolekcjonerskiej gry karcianej Magic: The Gathering. Dodaje pełne wsparcie czytników ekranu i nawigację klawiaturą do niemal każdego aspektu gry.

Mod obsługuje wszystkie języki, na które gra została przetłumaczona. Ponadto kilka języków, których sama gra nie obsługuje, jest częściowo pokrywanych: w tych językach tłumaczone są komunikaty specyficzne dla moda, takie jak teksty pomocy i podpowiedzi UI, natomiast dane kart i gry pozostają w domyślnym języku gry.

<h2>Czym jest Magic: The Gathering</h2>

Magic to kolekcjonerska gra karciana, znak towarowy Wizards of the Coast, w której grasz jako mag przeciwko innym magom, rzucając zaklęcia reprezentowane przez karty. W Magicu istnieje 5 kolorów, które reprezentują różne tożsamości rozgrywki i klimatu. Jeśli znasz Hearthstone'a lub Yu-Gi-Oh, rozpoznasz wiele pojęć, ponieważ Magic jest przodkiem wszystkich tych gier.
Jeśli chcesz dowiedzieć się więcej o Magicu ogólnie, oficjalna strona gry oraz wielu twórców treści pomogą ci.

<h2>Wymagania</h2>

- Windows 10 lub nowszy
- Magic: The Gathering Arena (zainstalowany za pomocą oficjalnego instalatora Wizards lub Steam)
- Czytnik ekranu (tylko NVDA i JAWS są testowane)
- MelonLoader (instalator zajmie się tym automatycznie)

<h2>Instalacja</h2>

<h3>Za pomocą instalatora (zalecane)</h3>

1. [Pobierz AccessibleArenaInstaller.exe](https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe) z najnowszego wydania na GitHubie
2. Zamknij MTG Arena, jeśli jest uruchomione
3. Uruchom instalator. Wykryje twoją instalację MTGA, w razie potrzeby zainstaluje MelonLoader i wdroży mod
4. Uruchom MTG Arena. Powinieneś usłyszeć "Accessible Arena v... launched" przez czytnik ekranu

<h3>Instalacja ręczna</h3>

1. Zainstaluj [MelonLoader](https://github.com/LavaGang/MelonLoader) w folderze MTGA
2. Pobierz `AccessibleArena.dll` z najnowszego wydania
3. Skopiuj DLL do folderu Mods w MTGA:
   - Instalacja WotC: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Instalacja Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Upewnij się, że `Tolk.dll` i `nvdaControllerClient64.dll` znajdują się w głównym folderze MTGA
5. Uruchom MTG Arena

<h2>Deinstalacja</h2>

Uruchom ponownie instalator. Jeśli mod jest już zainstalowany, zaproponuje opcję deinstalacji. Opcjonalnie możesz również usunąć MelonLoader. Aby odinstalować ręcznie, usuń `AccessibleArena.dll` z folderu `Mods\` i usuń `Tolk.dll` oraz `nvdaControllerClient64.dll` z głównego folderu MTGA.

<h2>Jeśli przychodzisz z Hearthstone'a</h2>

Jeśli grałeś w Hearthstone Access, z dobrych powodów rozpoznasz wiele rzeczy, ponieważ nie tylko zasady gry są do siebie podobne, ale też kierowałem się wieloma zasadami projektowania. Mimo to niektóre rzeczy są inne.

Najpierw masz więcej stref do poruszania się, bo Magic zna cmentarz, wygnanie i kilka dodatkowych stref. Twoje pole bitwy nie jest ograniczone co do rozmiaru i ma dodatkowe rzędy sortujące, aby mnogość rzeczy, które mogą się pojawić, była łatwiejsza do zarządzania.

Twoja mana nie rośnie automatycznie, lecz pochodzi z różnokolorowych kart ziem, które musisz aktywnie zagrywać. W związku z tym koszty many mają części bezbarwne i kolorowe, które zsumowane dają pełne wymogi kosztowe karty, które musisz spełnić.

Nie możesz atakować stworów bezpośrednio, celem atakujących mogą być tylko przeciwnicy i niektóre bardzo specyficzne karty (planeswalkerzy i bitwy). Jako obrońca musisz zdecydować, czy chcesz zablokować atak, aby stwory walczyły. Jeśli nie zablokujesz, obrażenia trafią twojego awatara gracza, ale twoje stwory mogą pozostać nietknięte. Ponadto obrażenia nie kumulują się na stworach, lecz są leczone pod koniec każdej tury, więc zarówno na koniec twojej tury, jak i przeciwnika. Aby wejść w interakcję ze stworami przeciwnika, które nie chcą z tobą walczyć, musisz zagrać konkretne karty lub tak mocno naciskać na sumę punktów życia przeciwnika, że nie będzie miał wyboru, jak tylko poświęcić cenne stwory, by przeżyć.

Gra ma bardzo wyraźnie wyodrębnione fazy walki, które pozwalają na konkretne akcje, takie jak dobieranie, rzucanie zaklęć czy walka. W związku z tym Magic pozwala i zachęca do działań w turach przeciwnika. Koniec z siedzeniem i czekaniem, aż coś się wydarzy. Graj interaktywną talią i niszcz plany przeciwnika na bieżąco.

<h2>Pierwsze kroki</h2>

Gra najpierw prosi o podanie pewnych danych o tobie i zarejestrowanie postaci. Powinno to działać za pomocą wewnętrznych mechanizmów gry, ale jeśli nie działa, możesz alternatywnie skorzystać ze strony gry, która jest w pełni dostępna.

Gra zaczyna się samouczkiem, w którym uczysz się podstaw Magic: The Gathering. Mod dodaje własne podpowiedzi samouczka dla użytkowników czytników ekranu obok standardowego samouczka. Po ukończeniu samouczka otrzymujesz w nagrodę 5 talii startowych, po jednej na każdy kolor.

Stamtąd masz kilka opcji, aby odblokować więcej kart i nauczyć się gry:

- **Wyzwania kolorów:** Zagraj wyzwanie koloru dla każdego z pięciu kolorów Magica. Każde wyzwanie polega na walce z 4 przeciwnikami NPC, a na końcu z prawdziwym graczem.
- **Wydarzenia z taliami startowymi:** Zagraj jedną z 10 dwukolorowych talii przeciwko prawdziwym ludziom, którzy mają dostępne te same opcje talii.
- **Jump In:** Wybierz dwa pakiety po 20 kart o różnych kolorach i tematach, połącz je w talię i graj przeciwko prawdziwym ludziom z podobnymi wyborami. Otrzymujesz darmowe żetony za to wydarzenie i zatrzymujesz wybrane karty.
- **Spark Ladder:** W pewnym momencie odblokowuje się Spark Ladder, w której grasz swoje pierwsze rankingowe mecze przeciwko prawdziwym przeciwnikom.

Sprawdzaj pocztę w menu społecznościowym, bo zawiera wiele nagród i paczek kart.

Gra stopniowo odblokowuje tryby w zależności od tego, co i ile grasz. Udziela ci wskazówek i zadań w menu postępów i celów oraz podświetla odpowiednie dla ciebie tryby w menu gry. Po ukończeniu wystarczająco dużej ilości treści dla nowego gracza wszystkie różne tryby i wydarzenia stają się w pełni dostępne.

W Kodeksie Multiversum możesz uczyć się o trybach gry i mechanikach. Rozszerza się on wraz z postępem w doświadczeniu NPE.

W ustawieniach konta możesz pominąć wszystkie doświadczenia samouczka i wymusić odblokowanie wszystkiego, aby od samego początku mieć pełną swobodę. Jednak granie wydarzeń dla nowych graczy daje dużo kart i jest zalecane dla nowych graczy. Odblokuj wszystko wcześnie tylko wtedy, gdy już wiesz, co robisz. W innym wypadku treść dla początkujących zapewnia dużo frajdy i nauki, jednocześnie dobrze cię prowadząc.

<h2>Skróty klawiszowe</h2>

Nawigacja wszędzie stosuje standardowe konwencje: strzałki do poruszania się, Home/End do skoku na pierwszy/ostatni, Enter do wyboru, Spacja do potwierdzenia, Backspace do cofnięcia lub anulowania. Tab/Shift+Tab również działa do nawigacji. Page Up/Page Down zmienia strony.

<h3>Globalne</h3>

- F1: Menu pomocy (wyświetla listę wszystkich skrótów dla bieżącego ekranu)
- Ctrl+F1: Ogłoszenie skrótów dla bieżącego ekranu
- F2: Ustawienia moda
- F3: Ogłoszenie bieżącego ekranu
- F4: Panel znajomych (z menu) / Czat pojedynku (podczas pojedynków)
- F5: Sprawdź / rozpocznij aktualizację
- Ctrl+R: Powtórz ostatni komunikat

<h3>Pojedynki - Strefy</h3>

Twoje strefy: C (Ręka), G (Cmentarz), X (Wygnanie), S (Stos), W (Strefa dowodzenia)
Strefy przeciwnika: Shift+G, Shift+X, Shift+W
Pole bitwy: B / Shift+B (Stwory), A / Shift+A (Ziemie), R / Shift+R (Niestwory)
W strefach: Lewo/Prawo do nawigacji, Góra/Dół do odczytania szczegółów karty, I dla rozszerzonych informacji
Shift+Góra/Dół: Przełączanie rzędów pola bitwy

<h3>Pojedynki - Informacje</h3>

- T: Tura/Faza
- L: Sumy życia
- V: Strefa info gracza
- D / Shift+D: Liczby kart w bibliotece
- Shift+C: Liczba kart w ręce przeciwnika
- M / Shift+M: Podsumowanie twoich / przeciwnika ziem
- K: Info o znacznikach na wybranej karcie
- O: Dziennik gry (ostatnie komunikaty pojedynku)
- E / Shift+E: Twój / przeciwnika licznik czasu

<h3>Pojedynki - Cele i akcje</h3>

- Tab / Ctrl+Tab: Cykliczne przełączanie celów (wszystkie / tylko przeciwnika)
- Enter: Wybierz cel
- Spacja: Przekaż priorytet, potwierdź atakujących/blokujących, przejdź do kolejnej fazy

<h3>Pojedynki - Full control i zatrzymania faz</h3>

- P: Przełącz full control (tymczasowy, resetuje się przy zmianie fazy)
- Shift+P: Przełącz zablokowany full control (stały)
- Shift+Backspace: Przełącz przekazanie do akcji przeciwnika (miękkie pominięcie)
- Ctrl+Backspace: Przełącz pominięcie tury (wymuś pominięcie całej tury)
- 1-0: Przełącz zatrzymania faz (1=Utrzymanie, 2=Dobór, 3=Pierwsza główna, 4=Początek walki, 5=Deklaracja atakujących, 6=Deklaracja blokujących, 7=Obrażenia w walce, 8=Koniec walki, 9=Druga główna, 0=Krok końcowy)

<h3>Pojedynki - Przeglądarki (Scry, Surveil, Mulligan)</h3>

- Tab: Nawigacja po wszystkich kartach
- C/D: Skok między strefą górną/dolną
- Enter: Przełącz umieszczenie karty

<h2>Rozwiązywanie problemów</h2>

<h3>Brak mowy po uruchomieniu gry</h3>

- Upewnij się, że czytnik ekranu działa przed uruchomieniem MTG Areny
- Sprawdź, czy `Tolk.dll` i `nvdaControllerClient64.dll` znajdują się w głównym folderze MTGA (instalator umieszcza je automatycznie)
- Sprawdź dziennik MelonLoader w folderze MTGA (`MelonLoader\Latest.log`) pod kątem błędów

<h3>Gra się zawiesza przy uruchamianiu lub mod się nie ładuje</h3>

- Upewnij się, że MelonLoader jest zainstalowany.
- Jeśli gra ostatnio otrzymała aktualizację, MelonLoader lub mod mogą wymagać ponownej instalacji. Uruchom instalator jeszcze raz.
- Sprawdź, czy `AccessibleArena.dll` znajduje się w folderze `Mods\` wewnątrz instalacji MTGA

<h3>Mod działał, ale przestał po aktualizacji gry</h3>

- Aktualizacje MTG Areny mogą nadpisać pliki MelonLoadera. Uruchom instalator jeszcze raz, aby ponownie zainstalować MelonLoader i mod.
- Jeśli gra znacznie zmieniła swoją wewnętrzną strukturę, mod może wymagać aktualizacji. Sprawdź nowe wydania na GitHubie.

<h3>Skróty klawiszowe nie działają</h3>

- Upewnij się, że okno gry ma fokus (kliknij na nie lub przełącz Alt+Tabem)
- Naciśnij F1, aby sprawdzić, czy mod jest aktywny. Jeśli słyszysz menu pomocy, mod działa.
- Niektóre skróty działają tylko w określonych kontekstach (skróty pojedynków działają tylko podczas pojedynku)

<h3>Zły język</h3>

- Naciśnij F2, aby otworzyć menu ustawień, a następnie użyj Enter, aby przełączać języki

<h3>Windows ostrzega, że instalator lub DLL jest niebezpieczny</h3>

Instalator i DLL moda nie są podpisane cyfrowo. Certyfikaty podpisu kodu kosztują kilkaset euro rocznie, co nie jest realne dla darmowego projektu dostępnościowego. W rezultacie Windows SmartScreen i niektóre antywirusy ostrzegą cię przy pierwszym uruchomieniu instalatora lub oznaczą DLL jako "nieznany wydawca".

Aby sprawdzić, czy pobrany plik jest zgodny z tym opublikowanym na GitHubie, każde wydanie zawiera sumę kontrolną SHA256 zarówno dla `AccessibleArenaInstaller.exe`, jak i `AccessibleArena.dll`. Możesz obliczyć hash pobranego pliku i porównać:

- PowerShell: `Get-FileHash <nazwapliku> -Algorithm SHA256`
- Wiersz polecenia: `certutil -hashfile <nazwapliku> SHA256`

Jeśli hash zgadza się z tym z informacji o wydaniu, plik jest autentyczny. Aby uruchomić instalator pomimo ostrzeżenia SmartScreen, wybierz "Więcej informacji", a następnie "Uruchom mimo to".

<h2>Zgłaszanie błędów</h2>

Jeśli znalazłeś błąd, możesz opublikować post tam, gdzie znalazłeś mod, lub [otworzyć zgłoszenie na GitHubie](https://github.com/JeanStiletto/AccessibleArena/issues).

Dołącz następujące informacje:

- Co robiłeś, gdy wystąpił błąd
- Czego oczekiwałeś
- Co faktycznie się wydarzyło
- Jeśli chcesz dołączyć dziennik gry, zamknij grę i udostępnij plik dziennika MelonLoader z folderu MTGA:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Znane problemy</h2>
Gra powinna pokrywać niemal każdy ekran gry, ale mogą zdarzyć się przypadki brzegowe, które nie działają w pełni. PayPal blokuje niewidomych użytkowników nielegalnym niedźwiękowym captcha, więc musisz skorzystać z pomocy osoby widzącej lub innych metod płatności, jeśli chcesz wydać prawdziwe pieniądze w grze.
Niektóre konkretne wydarzenia mogą nie działać w pełni. Draft z prawdziwymi graczami ma jeszcze nieobsługiwany ekran poczekalni, ale w quickdraft wybierasz karty przeciwko botom, zanim zmierzysz się z ludzkimi przeciwnikami, ten tryb działa i jest polecany każdemu, kto lubi tego rodzaju doświadczenie. Tryb Cube nie jest objęty. Nawet nie wiem, o co w nim dokładnie chodzi, a kosztuje dużo zasobów w grze. Zajmę się tym, jeśli będę miał czas lub na prośbę.
System kosmetyczny gry z emotkami, zwierzakami, stylami kart i tytułami jest obecnie wspierany tylko częściowo.
Mod jest testowany tylko na Windowsie z NVDA i JAWS i wciąż polega na niezmodyfikowanej bibliotece Tolk. Nie mogę tutaj testować zgodności z Makiem lub Linuksem, a biblioteki wieloplatformowe takie jak Prism nie wspierały w pełni starych wersji .NET, od których gra zależy w tym momencie. Przejdę więc na szerszą bibliotekę tylko wtedy, gdy ktoś pomoże w testowaniu dla innych platform lub azjatyckich czytników ekranu, które nie są w pełni wspierane przez niezmodyfikowany Tolk. Więc nie wahaj się ze mną skontaktować, jeśli chcesz, żebym nad tym pracował.

Aktualna lista znanych problemów znajduje się w [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

<h2>Zastrzeżenia</h2>
<h3>Inne rodzaje dostępności</h3>

Ten mod nazywa się Accessible Arena głównie dlatego, że brzmi dobrze. Ale w tej chwili jest to tylko mod dostępności dla czytników ekranu. Jestem absolutnie zainteresowany objęciem tym modem większej liczby niepełnosprawności, upośledzeń wzroku, niepełnosprawności motorycznych itp. Ale mam doświadczenie tylko w dostępności dla czytników ekranu. Jako osoba całkowicie niewidoma na przykład kwestie kolorów i czcionek są dla mnie całkowicie abstrakcyjne. Więc jeśli chcesz, aby coś takiego zostało zaimplementowane, nie wahaj się ze mną skontaktować, jeśli potrafisz jasno opisać swoje potrzeby i jesteś gotów pomóc mi testować wyniki.
Wtedy z radością uczynię nazwę tego moda bardziej prawdziwą.

<h3>Kontakt z firmą</h3>

Niestety nie udało mi się uzyskać wiarygodnych informacji o zespole Areny ani nieformalnych kontaktów z deweloperami. Postanowiłem więc na razie pominąć ich oficjalne kanały komunikacji. Przez 3 miesiące tworzenia i grania nigdy nie trafiłem na system ochrony antybotowej, więc nie sądzę, żeby mogli nas wykryć jako użytkowników moda. Ale nie chciałem ryzykować komunikacji na oficjalnych kanałach jako pojedyncza osoba. Więc rozprzestrzeniajcie wieść o modzie i budujmy dużą, wartościową społeczność. Wtedy będziemy mieli dużo lepszą pozycję, jeśli zdecydujemy się skontaktować bezpośrednio. Tylko nie próbujcie do nich pisać bez wcześniejszego skontaktowania się ze mną. Szczególnie nie wysyłajcie im próśb o natywną dostępność lub integrację mojego moda z ich bazą kodu. Żadne z tych nie nastąpi w żadnym wypadku.

<h3>Zakupy w grze</h3>

Arena ma kilka mechanik związanych z prawdziwymi pieniędzmi i można kupić walutę w grze. Te metody płatności są w większości dostępne, z wyjątkiem PayPal, ponieważ dołączyli ochronę captcha do swojego logowania. Możesz spróbować odinstalować mod na czas rejestracji metody płatności i poprosić o pomoc osobę widzącą, ale nawet to jest niewiarygodne ze względu na ich koszmar dostępności w postaci captcha, jeszcze bardziej zepsutej i źle zaimplementowanej przez Wizards of the Coast.
Ale inne metody płatności działają stabilnie. Ja i inni przetestowaliśmy zakupy w grze i korzystanie z systemu powinno być bezpieczne. Ale jest absolutnie możliwe, że wystąpią błędy, a nawet że mod wprowadzi cię w błąd. Może kliknąć nie to, co trzeba, wyświetlić złe lub niekompletne informacje, zrobić nie to, co trzeba, z powodu wewnętrznych zmian w Arenie. Mogę to przetestować, ale nie mogę w 100% zagwarantować, że nie kupisz niewłaściwych rzeczy za prawdziwe pieniądze. Nie wezmę za to odpowiedzialności, a ze względu na fakt, że nie jest to oficjalny produkt Areny, firma gry również tego nie zrobi. W takim przypadku nawet nie próbuj otrzymać zwrotu pieniędzy, nie zrobią tego.

<h3>Użycie AI</h3>

Kod tego moda został stworzony w 100% z pomocą agenta Claude firmy Anthropic, używając modeli Opus: zaczęło się na 4.5, większość rozwoju odbyła się na 4.6, a ostatnie kroki do wydania zostały wykonane na 4.7. I dzięki mojemu największemu współtwórcy, trochę Codexa też. Jestem świadomy problemów z użyciem AI. Ale w czasie, gdy wszyscy używają tych programów do wielu o wiele bardziej podejrzanych rzeczy, podczas gdy branża gier nie może dać nam dostępności, której chcemy, pod względem jakości czy ilości, zdecydowałem się jednak użyć tych narzędzi.

<h2>Jak wnieść wkład</h2>

Chętnie przyjmę wkład i dzięki [blindndangerous](https://github.com/blindndangerous) wiele pomocnej pracy innej osoby już jest częścią tego moda. Szczególnie interesują mnie ulepszenia i poprawki rzeczy, których nie mogę testować, takie jak różne konfiguracje systemów, poprawianie języków, których nie znam itp. Ale przyjmuję również prośby o nowe funkcje. Przed rozpoczęciem pracy sprawdź znane problemy.

- Ogólne wytyczne dotyczące wkładu znajdują się w [CONTRIBUTING.md](../CONTRIBUTING.md)
- Pomoc przy tłumaczeniach - zobacz [CONTRIBUTING_TRANSLATIONS.md](CONTRIBUTING_TRANSLATIONS.md)

<h2>Podziękowania</h2>

A teraz chcę podziękować wielu osobom, bo na szczęście to nie był tylko ja i AI w czarnej skrzynce, lecz cała sieć wokół mnie, pomagająca, wzmacniająca, po prostu społeczna i miła.
Napisz do mnie na DM, jeśli cię zapomniałem lub chcesz być znany pod innym imieniem lub nie chcesz być wymieniony.

Po pierwsze, ta praca opiera się bardzo mocno na pracy innych osób, które wykonały pionierskie rzeczy, które ja po prostu musiałem powtórzyć dla Accessible Arena.
Pod względem projektowym to Hearthstone Access, po którym mogłem wiele odziedziczyć, nie tylko dlatego, że jest dobrze znany każdemu, kto grał w tę grę, ale dlatego, że to naprawdę dobry projekt UI.
Pod względem moddingu chcę podziękować członkom Discorda moddingowego Zaxa. Nie tylko rozpracowaliście wszystkie te rzeczy, wszystkie narzędzia i procedury, które ja musiałem tylko zainstalować i używać. Nauczyliście mnie wszystkiego, co muszę wiedzieć o moddingu z AI, bądź bezpośrednio, bądź omawiając rzeczy publicznie lub pomagając innym początkującym. Dodatkowo daliście mi platformę i społeczność, w której ja i mój projekt możemy istnieć.

Za ogromne wkłady kodowe chcę podziękować [blindndangerous](https://github.com/blindndangerous), który również wykonał wiele pracy nad tym projektem. Przez cały okres życia projektu myślę, że dostałem od niego jakieś 50 PR i więcej w sprawie wszelkich typów problemów od małych irytujących rzeczy do rozpracowania po większe sugestie UI i dostępność całych ekranów gry.
Dalsze podziękowania dla Ahixa, który stworzył [prompty refaktoryzacyjne dla dużych projektów kodowanych przez AI](https://github.com/ahicks92/llm-mod-refactoring-prompts), które uruchomiłem na wierzchu własnych refaktoryzacji, aby zapewnić jakość i utrzymywalność kodu.

Za wkład w kod chcę podziękować:
- [blindndangerous](https://github.com/blindndangerous)
- [LordLuceus](https://github.com/LordLuceus)

Za testowanie bet, opinie i pomysły chcę podziękować:
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
- [LordLuceus](https://github.com/LordLuceus)

Za testowanie przez osoby widzące w celu zrozumienia wizualnych przepływów pracy i potwierdzenia niektórych rzeczy chcę podziękować:
- [mauriceKA](https://github.com/mauriceKA)
- VeganWolf
- Lea Holstein

<h3>Użyte narzędzia</h3>

- Claude ze wszystkimi zawartymi modelami
- MelonLoader
- Harmony do patchowania IL
- Tolk do komunikacji z czytnikami ekranu
- ILSpy do dekompilacji kodu gry

<h2>Wesprzyj swojego moddera</h2>

Stworzenie tego moda nie tylko sprawiło mi wiele radości i dało mi siłę, ale również kosztowało mnie naprawdę wiele czasu i prawdziwych pieniędzy na subskrypcje Claude. Zachowam je, aby pracować nad dalszymi ulepszeniami i utrzymaniem projektu przez kolejne lata.
Więc jeśli chcesz i możesz sobie pozwolić na jednorazową lub nawet comiesięczną darowiznę, możesz zajrzeć tutaj.
Bardzo doceniam to uznanie dla mojej pracy i daje mi to stabilną podstawę do kontynuowania pracy nad Areną i, mam nadzieję, innymi dużymi projektami w przyszłości.

[Ko-fi: ko-fi.com/jeanstiletto](https://ko-fi.com/jeanstiletto)

<h2>Licencja</h2>

Ten projekt jest licencjonowany na licencji GNU General Public License v3.0. Szczegóły w pliku LICENSE.

<h2>Linki</h2>

- [GitHub](https://github.com/JeanStiletto/AccessibleArena)
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [MTG Arena](https://magic.wizards.com/mtgarena)

<h2>Inne języki</h2>

[English](../README.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Français](README.fr.md) | [Italiano](README.it.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Português (Brasil)](README.pt-BR.md) | [Русский](README.ru.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md)

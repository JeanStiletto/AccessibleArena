# Accessible Arena

Mod dostępności dla Magic: The Gathering Arena umożliwiający niewidomym i słabowidzącym graczom grę za pomocą czytnika ekranu. Pełna nawigacja klawiaturowa, komunikaty czytnika ekranu dla wszystkich stanów gry i lokalizacja w 12 językach.

**Status:** Publiczna beta. Główna rozgrywka jest funkcjonalna. Pozostają pewne przypadki szczególne i drobne błędy. Zobacz Znane problemy poniżej.

**Uwaga:** Obecnie tylko klawiatura. Brak obsługi myszy lub ekranu dotykowego. Testowano tylko na Windows 11 z NVDA. Inne wersje Windows i czytniki ekranu (JAWS, Narrator itp.) mogą działać, ale nie były testowane.

## Funkcje

- Pełna nawigacja klawiaturowa dla wszystkich ekranów (ekran główny, sklep, mistrzostwo, konstruktor talii, pojedynki)
- Integracja z czytnikiem ekranu za pośrednictwem biblioteki Tolk
- Odczytywanie informacji o kartach za pomocą strzałek (nazwa, koszt many, typ, siła/wytrzymałość, tekst zasad, tekst fabularny, rzadkość, artysta)
- Pełne wsparcie pojedynków: nawigacja po strefach, walka, wybór celów, stos, przeglądarki (wróżenie, inwigilacja, mulligan)
- Ogłoszenia o relacjach przyłączenia i walki (zaczarowany przez, blokuje, cel)
- Dostępny sklep z opcjami zakupu i obsługą okien płatności
- Wsparcie dla meczów z botami do ćwiczeń
- Menu ustawień (F2) i menu pomocy (F1) dostępne wszędzie
- 12 języków: angielski, niemiecki, francuski, hiszpański, włoski, portugalski (BR), japoński, koreański, rosyjski, polski, chiński uproszczony, chiński tradycyjny

## Wymagania

- Windows 10 lub nowszy
- Magic: The Gathering Arena (zainstalowana przez oficjalny instalator lub Epic Games Store)
- Czytnik ekranu (zalecany NVDA: https://www.nvaccess.org/download/)
- MelonLoader (instalator obsługuje to automatycznie)

## Instalacja

### Za pomocą instalatora (zalecane)

1. Pobierz `AccessibleArenaInstaller.exe` z najnowszego wydania na GitHub: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Zamknij MTG Arena, jeśli jest uruchomiona
3. Uruchom instalator. Wykryje on instalację MTGA, zainstaluje MelonLoader jeśli to konieczne i wdroży mod
4. Uruchom MTG Arena. Powinieneś usłyszeć „Accessible Arena v... uruchomiono" przez czytnik ekranu

### Instalacja ręczna

1. Zainstaluj MelonLoader w folderze MTGA (https://github.com/LavaGang/MelonLoader)
2. Pobierz `AccessibleArena.dll` z najnowszego wydania
3. Skopiuj DLL do: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
4. Upewnij się, że `Tolk.dll` i `nvdaControllerClient64.dll` znajdują się w folderze głównym MTGA
5. Uruchom MTG Arena

## Szybki start

Jeśli nie masz jeszcze konta Wizards, możesz je utworzyć na https://myaccounts.wizards.com/ zamiast korzystać z ekranu rejestracji w grze.

Po instalacji uruchom MTG Arena. Mod ogłasza bieżący ekran przez czytnik ekranu.

- Naciśnij **F1** w dowolnym momencie, aby otworzyć nawigowalne menu pomocy z listą wszystkich skrótów klawiszowych
- Naciśnij **F2**, aby otworzyć menu ustawień (język, szczegółowość, komunikaty samouczka)
- Naciśnij **F3**, aby usłyszeć nazwę bieżącego ekranu
- Użyj **Strzałek góra/dół** lub **Tab/Shift+Tab** do nawigacji po menu
- Naciśnij **Enter** lub **Spację**, aby aktywować elementy
- Naciśnij **Backspace**, aby wrócić

## Skróty klawiszowe

### Menu

- Strzałka góra/dół (lub W/S): Nawigacja po elementach
- Tab/Shift+Tab: Nawigacja po elementach (jak strzałki góra/dół)
- Strzałka lewo/prawo (lub A/D): Sterowanie karuzelą i krokowcem
- Home/End: Przejdź do pierwszego/ostatniego elementu
- Page Up/Page Down: Poprzednia/następna strona w kolekcji
- Enter/Spacja: Aktywuj
- Backspace: Wróć

### Pojedynki - Strefy

- C: Twoja ręka
- G / Shift+G: Twój cmentarz / Cmentarz przeciwnika
- X / Shift+X: Twoje wygnanie / Wygnanie przeciwnika
- S: Stos
- B / Shift+B: Twoje stwory / Stwory przeciwnika
- A / Shift+A: Twoje lądy / Lądy przeciwnika
- R / Shift+R: Twoje nie-stwory / Nie-stwory przeciwnika

### Pojedynki - Wewnątrz stref

- Lewo/Prawo: Nawigacja po kartach
- Home/End: Przejdź do pierwszej/ostatniej karty
- Strzałka góra/dół: Odczytaj szczegóły karty, gdy jest na niej fokus
- I: Rozszerzone informacje o karcie (opisy słów kluczowych, inne strony)
- Shift+Góra/Dół: Przełączanie rzędów pola bitwy

### Pojedynki - Informacje

- T: Bieżąca tura i faza
- L: Łączne punkty życia
- V: Strefa informacji o graczu (Lewo/Prawo do zmiany gracza, Góra/Dół do właściwości)
- D / Shift+D: Liczba kart w twojej bibliotece / Bibliotece przeciwnika
- Shift+C: Liczba kart w ręce przeciwnika

### Pojedynki - Akcje

- Spacja: Potwierdź (przekaż priorytet, potwierdź atakujących/blokujących, następna faza)
- Backspace: Anuluj / odrzuć
- Tab: Przełączaj cele lub podświetlone elementy
- Ctrl+Tab: Przełączaj tylko cele przeciwnika
- Enter: Wybierz cel

### Pojedynki - Przeglądarki (Wróżenie, Inwigilacja, Mulligan)

- Tab: Nawigacja po wszystkich kartach
- C/D: Przejdź do górnej/dolnej strefy
- Lewo/Prawo: Nawigacja wewnątrz strefy
- Enter: Przełącz umiejscowienie karty
- Spacja: Potwierdź wybór
- Backspace: Anuluj

### Globalne

- F1: Menu pomocy
- F2: Menu ustawień
- F3: Ogłoś bieżący ekran
- Ctrl+R: Powtórz ostatni komunikat
- Backspace: Uniwersalne wróć/zamknij/anuluj

## Zgłaszanie błędów

Jeśli znajdziesz błąd, otwórz issue na GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Dołącz następujące informacje:

- Co robiłeś, gdy wystąpił błąd
- Czego się spodziewałeś
- Co faktycznie się stało
- Twój czytnik ekranu i jego wersja
- Dołącz plik dziennika MelonLoader: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`

## Znane problemy

- Klawisz Spacja do przekazania priorytetu nie zawsze działa niezawodnie (mod klika przycisk bezpośrednio jako awaryjne rozwiązanie)
- Karty na liście talii w konstruktorze pokazują tylko nazwę i ilość, nie pełne szczegóły
- Wybór typu kolejki PlayBlade (Rankingowa, Gra otwarta, Brawl) nie zawsze ustawia prawidłowy tryb gry

Pełna lista w docs/KNOWN_ISSUES.md.

## Rozwiązywanie problemów

**Brak mowy po uruchomieniu gry**
- Upewnij się, że czytnik ekranu jest uruchomiony przed uruchomieniem MTG Arena
- Sprawdź, czy `Tolk.dll` i `nvdaControllerClient64.dll` znajdują się w folderze głównym MTGA (instalator umieszcza je automatycznie)
- Sprawdź dziennik MelonLoader w `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log` pod kątem błędów

**Gra się zawiesza przy uruchamianiu lub mod się nie ładuje**
- Upewnij się, że MelonLoader jest zainstalowany.
- Jeśli gra została niedawno zaktualizowana, może być konieczna ponowna instalacja MelonLoader lub moda. Uruchom instalator ponownie.
- Sprawdź, czy `AccessibleArena.dll` znajduje się w `C:\Program Files\Wizards of the Coast\MTGA\Mods\`

**Mod działał, ale przestał po aktualizacji gry**
- Aktualizacje MTG Arena mogą nadpisać pliki MelonLoader. Uruchom instalator ponownie, aby zainstalować ponownie MelonLoader i mod.
- Jeśli gra znacząco zmieniła swoją wewnętrzną strukturę, mod może wymagać aktualizacji. Sprawdź nowe wydania na GitHub.

**Skróty klawiszowe nie działają**
- Upewnij się, że okno gry jest aktywne (kliknij na nie lub użyj Alt+Tab)
- Naciśnij F1, aby sprawdzić, czy mod jest aktywny. Jeśli słyszysz menu pomocy, mod działa.
- Niektóre skróty działają tylko w określonych kontekstach (skróty pojedynków tylko podczas pojedynku)

**Zły język**
- Naciśnij F2, aby otworzyć menu ustawień, następnie użyj Enter, aby przełączać języki

## Budowanie ze źródeł

Wymagania: .NET SDK (dowolna wersja obsługująca cel net472)

```
git clone https://github.com/JeanStiletto/AccessibleArena.git
cd AccessibleArena
dotnet build src/AccessibleArena.csproj
```

Zbudowana DLL będzie w `src/bin/Debug/net472/AccessibleArena.dll`.

Referencje do zestawów gry są oczekiwane w folderze `libs/`. Skopiuj te DLL z instalacji MTGA (`MTGA_Data/Managed/`):
- Assembly-CSharp.dll
- Core.dll
- UnityEngine.dll, UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll, UnityEngine.InputLegacyModule.dll
- Unity.TextMeshPro.dll, Unity.InputSystem.dll
- Wizards.Arena.Models.dll, Wizards.Arena.Enums.dll, Wizards.Mtga.Metadata.dll, Wizards.Mtga.Interfaces.dll
- ZFBrowser.dll

DLL MelonLoader (`MelonLoader.dll`, `0Harmony.dll`) pochodzą z instalacji MelonLoader.

## Licencja

Ten projekt jest licencjonowany na warunkach GNU General Public License v3.0. Szczegóły w pliku LICENSE.

## Linki

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- Czytnik ekranu NVDA (zalecany): https://www.nvaccess.org/download/
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena

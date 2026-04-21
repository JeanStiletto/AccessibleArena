<h1>Accessible Arena</h1>

<h2>What is this mod</h2>

This mod allows you to play Arena, the most popular and beginner-friendly digital representation of the TCG Magic: The Gathering. It adds full screen reader support and keyboard navigation to nearly every aspect of the game.

The mod supports all languages the game is translated in. Additionally, a few languages that the game itself does not support are partially covered: in those, mod-specific announcements like help texts and UI hints are translated, while card and game data remain in the game's default language.

<h2>What is Magic: The Gathering</h2>

Magic is a trading card game trademarked by Wizards of the Coast that allows to play as a mage against other mages, casting spells represented by the cards. There exist 5 colours in Magic that represent different identities of gameplay and flavour. If you are familiar with Hearthstone or Yu-Gi-Oh you will recognize a lot of concepts cause Magic is the ancestor of all those games.
If you want to learn more about Magic in general, the game's website as well as a lot of content creators will help you out.

<h2>Requirements</h2>

- Windows 10 or later
- Magic: The Gathering Arena (installed via the official Wizards installer or Steam)
- A screen reader (only NVDA and JAWS are tested)
 - MelonLoader (the installer handles this automatically)

<h2>Installation</h2>

<h3>Using the installer (recommended)</h3>

1. Download `AccessibleArenaInstaller.exe` from the latest release on GitHub: https://github.com/JeanStiletto/AccessibleArena/releases/latest/download/AccessibleArenaInstaller.exe
2. Close MTG Arena if it is running
3. Run the installer. It will detect your MTGA installation, install MelonLoader if needed, and deploy the mod
4. Launch MTG Arena. You should hear "Accessible Arena v... launched" through your screen reader

<h3>Manual installation</h3>

1. Install MelonLoader into your MTGA folder (https://github.com/LavaGang/MelonLoader)
2. Download `AccessibleArena.dll` from the latest release
3. Copy the DLL to your MTGA Mods folder:
   - WotC install: `C:\Program Files\Wizards of the Coast\MTGA\Mods\`
   - Steam install: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\Mods\`
4. Ensure `Tolk.dll` and `nvdaControllerClient64.dll` are in the MTGA root folder
5. Launch MTG Arena

<h2>Uninstallation</h2>

Run the installer again. If the mod is already installed, it will offer an uninstall option. You can optionally remove MelonLoader as well. To uninstall manually, delete `AccessibleArena.dll` from the `Mods\` folder and remove `Tolk.dll` and `nvdaControllerClient64.dll` from the MTGA root folder.

<h2>If you come from Hearthstone</h2>

If you have played Hearthstone Access you will recognize a lot of things for good reasons, cause not just game principles are close to each other but cause I followed a lot of design principles. Still some things are different.

First you have more zones to navigate, cause Magic knows graveyard, exile and some extra zones. Your battlefield isn't limited in size and has additional sorting rows to make the mass of things that can appear more manageable.

Your mana isn't rising automatically but comes from different colored land cards you have actively to play. Regarding this mana costs have colorless and colored parts that added together give the full cost requirements of a card you have to fulfill.

You can not attack creatures directly, only opponents and some very specific cards (planeswalkers and battles) can be targeted by attackers. As defender you have to decide if you want to block an attack to make creatures fight. If you don't block the damage will hit your player avatar but your creatures can live untouched. Further damage doesn't accumulate on creatures but is healed at the end of each turn, so as well at the end of your and the opponent's turn. To interact with opponent's creatures that refuse to fight with you, you must play specific cards or pressure life total of your opponent so hard they have no choice to sacrifice valuable creatures to survive.

The game has very distinguished battle phases that allow specific actions like drawing, casting spells or fighting. Regarding this Magic allows and empowers you to do things on opponent's turns. No longer sit there and wait while things happen. Play an interactive deck and destroy enemy plans on the fly.

<h2>First steps</h2>

The game first asks you to give some data about you and to register a character. This should work via game internals but if it doesn't you can alternatively use the game's website to do this, it is fully accessible.

The game starts with a tutorial where you learn the basics of Magic: The Gathering. The mod adds custom tutorial hints for screen reader users alongside the standard tutorial. After finishing the tutorial, you get rewarded with 5 starter decks, one for each color.

From there, you have several options to unlock more cards and learn the game:

- **Color Challenges:** Play through the color challenge for each of the five Magic colors. Each challenge has you fight 4 NPC opponents, followed by a match against a real player at the end.
- **Starter Deck Events:** Play one of 10 two-colored decks against real humans who have the same deck choices available.
- **Jump In:** Choose two 20-card packages of different colors and themes, combine them into a deck, and play against real humans with similar choices. You get free tokens for this event and keep the cards you chose.
- **Spark Ladder:** At some point the spark ladder unlocks, where you play your first ranked matches against real opponents.

Check your mail under the social menu as those contain a lot of rewards and card packs.

The game unlocks modes gradually based on what and how much you play. It gives you hints and quests in the progress and objectives menu, and highlights relevant modes for you under the play menu. Once you finish enough of the new player content, all different modes and events become fully available.

In the Codex of the Multiverse you can learn about game modes and mechanics. It extends with growing progress in the NPE experience.

Under settings account you can skip all tutorial experiences and force-unlock everything to have full freedom from the very beginning. However, playing the new player events gives you a lot of cards and is recommended for new players. Only unlock everything early if you already know what you are doing. Otherwise the beginner content provides plenty of fun and learning while guiding you well.

<h2>Keyboard shortcuts</h2>

Navigation follows standard conventions throughout: Arrow keys to move, Home/End to jump to first/last, Enter to select, Space to confirm, Backspace to go back or cancel. Tab/Shift+Tab also works for navigation. Page Up/Page Down changes pages.

<h3>Global</h3>

- F1: Help menu (lists all shortcuts for the current screen)
- Ctrl+F1: Announce shortcuts for the current screen
- F2: Settings menu
- F3: Announce current screen
- Ctrl+R: Repeat last announcement

<h3>Duels - Zones</h3>

Your zones: C (Hand), G (Graveyard), X (Exile), S (Stack), W (Command Zone)
Opponent zones: Shift+G, Shift+X, Shift+W
Battlefield: B / Shift+B (Creatures), A / Shift+A (Lands), R / Shift+R (Non-creatures)
Within zones: Left/Right to navigate, Up/Down to read card details, I for extended info
Shift+Up/Down: Switch battlefield rows

<h3>Duels - Information</h3>

T (Turn/Phase), L (Life totals), V (Player info zone), D / Shift+D (Library counts), Shift+C (Opponent hand count)

<h3>Duels - Targeting and actions</h3>

- Tab / Ctrl+Tab: Cycle targets (all / opponent only)
- Enter: Select target
- Space: Pass priority, confirm attackers/blockers, advance phase

<h3>Duels - Browsers (Scry, Surveil, Mulligan)</h3>

- Tab: Navigate all cards
- C/D: Jump between top/bottom zones
- Enter: Toggle card placement

<h2>Troubleshooting</h2>

<h3>No speech output after launching the game</h3>

- Make sure your screen reader is running before launching MTG Arena
- Check that `Tolk.dll` and `nvdaControllerClient64.dll` are in the MTGA root folder (the installer places them automatically)
- Check the MelonLoader log in your MTGA folder (`MelonLoader\Latest.log`) for errors

<h3>Game crashes on startup or mod not loading</h3>

- Make sure MelonLoader is installed.
- If the game updated recently, MelonLoader or the mod may need to be reinstalled. Run the installer again.
- Check that `AccessibleArena.dll` is in the `Mods\` folder inside your MTGA installation

<h3>Mod was working but stopped after a game update</h3>

- MTG Arena updates can overwrite MelonLoader files. Run the installer again to reinstall both MelonLoader and the mod.
- If the game changed its internal structure significantly, the mod may need an update. Check for new releases on GitHub.

<h3>Keyboard shortcuts not working</h3>

- Make sure the game window is focused (click on it or Alt+Tab to it)
- Press F1 to check if the mod is active. If you hear the help menu, the mod is running.
- Some shortcuts only work in specific contexts (duel shortcuts only work during a duel)

<h3>Wrong language</h3>

- Press F2 to open the settings menu, then use Enter to cycle through languages

<h3>Windows warns about the installer or the DLL being unsafe</h3>

The installer and the mod DLL are not code-signed. Code-signing certificates cost a few hundred euros per year, which is not realistic for a free accessibility project. As a result, Windows SmartScreen and some antivirus tools will warn you when running the installer for the first time, or flag the DLL as "unknown publisher."

To verify the file you downloaded matches the one published on GitHub, each release lists a SHA256 checksum for both `AccessibleArenaInstaller.exe` and `AccessibleArena.dll`. You can compute the hash of your downloaded file and compare:

- PowerShell: `Get-FileHash <filename> -Algorithm SHA256`
- Command Prompt: `certutil -hashfile <filename> SHA256`

If the hash matches the one in the release notes, the file is genuine. To run the installer past the SmartScreen warning, choose "More info" and then "Run anyway."

<h2>Reporting bugs</h2>

If you find a bug you have several options.
Post in the place you found the mod published.
Join the accessible modding Discord (link todo) where there is a channel for Arena.
Open an issue on GitHub: https://github.com/JeanStiletto/AccessibleArena/issues

Include the following information:

- What you were doing when the bug occurred
- What you expected to happen
- What actually happened
- If you want to attach a game log, close the game and share the MelonLoader log file from your MTGA folder:
  - WotC: `C:\Program Files\Wizards of the Coast\MTGA\MelonLoader\Latest.log`
  - Steam: `C:\Program Files (x86)\Steam\steamapps\common\MTGA\MelonLoader\Latest.log`

<h2>Known issues</h2>
The game should cover nearly every screen in the game but there might be some edge cases not fully functioning. PayPal blocks blind users with an illegal non-audio captcha so you have to use sighted help or other payment methods if you wanna spend real money on the game.
Some specific events might not be fully working. Drafting with real players has a lobby screen not supported yet, but in quickdraft you pick cards against bots before you face human opponents, this is functional and a recommended mode for everyone who likes this kind of experience. Cube mode is untouched. I don't even really know what this is about and it costs a lot of in-game resources. So I will do this if I have time or on request.
The cosmetics system of the game with Emotes, Pets, card styles and titles is only partly supported yet.
The mod is only tested on Windows with NVDA and JAWS and still relies on the unmodified Tolk library. I cannot test Mac or Linux compatibility here, and cross-platform libraries like Prism didn't fully support the old .NET versions the game depends on at this point. So I will only switch to a broader library if people can help out with testing for either other platforms or Asian screen readers that aren't fully supported by unmodified Tolk. So don't hesitate to contact me if you want me working on this.


For the current list of known issues, see [docs/KNOWN_ISSUES.md](docs/KNOWN_ISSUES.md).

<h2>Disclaimers</h2>

<h3>Other accessibilities</h3>

This mod calls itself Accessible Arena mostly cause it sounds good. But at the moment this is only a screen reader accessibility mod. I am absolutely interested in covering more disabilities with this mod, visual impairments, motoric disabilities etc. But I am only experienced in screen reader accessibility. As fully blind person for example questions of coloring and fonts are fully abstract to me. So if you want something in this kind implemented please don't hesitate to contact me if you can clearly describe what your needs are and are willing to help me test the results.
Then I am happy to give the name of this mod more truth.

<h3>In-game purchases</h3>

Arena has some real money mechanics and you can buy an in-game currency. Those payment methods are mostly accessible except for PayPal cause they included illegal captchas without audio alternatives. I and others tested in-game purchasing of things and it should be safe to use the system. But it is absolutely possible that there will occur bugs or even that the mod will mislead you. Could click on the wrong things, show you wrong or incomplete information, do the wrong things due to internal changes of Arena. I could test it but I cannot 100% guarantee that you could buy the wrong things with your real money. I won't take responsibility for this and due to the fact that this is no official Arena product the game company won't do this as well. Please don't even try to get refunds in this case they won't give you those.

<h3>AI use</h3>

The code of this mod is 100% created with the help of Anthropic's Claude agent using the Opus models: it started on 4.5, most of the development happened on 4.6, and the final steps toward release were done on 4.7. And thanks to my biggest contributor a bit of Codex as well. I am aware of the problems with AI use. But in a time where everyone uses those software to do a lot of way more shady things while gaming industry couldn't give us the accessibility we want in terms of quality or quantity I still decided to use the tools.

<h2>How to contribute</h2>

I am happy to take contributions and with [blindndangerous](https://github.com/blindndangerous) already a lot of helpful work of another person is part of this mod. I am especially interested in improvement and fixes for things I cannot test like different system configurations, fixing languages I don't speak etc. But take feature requests as well. Before you work on something check known issues.

- For general contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md)
- For translation help, see [docs/CONTRIBUTING_TRANSLATIONS.md](docs/CONTRIBUTING_TRANSLATIONS.md)

<h2>Credits</h2>

And now I want to thank a whole lot of people, cause thankfully this was not just me and the AI in a black box but a whole network around me, helping out, empowering, just being social and nice.
Please DM me if I forgot you or if you want to be known under a different name or not mentioned.

First this work is grounded very much on the work of other people who did the pioneer things I just have to redo for accessible arena.
In terms of design this is Hearthstone Access I could inherit a lot not just cause it's well known for everyone who played the game but cause it's really good UI design.
In terms of modding I want to thank the members of Zax's modding Discord. You not just figured all the stuff out all the tools and procedures out I just had to install and use. You taught me everything I have to know about AI modding either directly or by discussing things in public or helping other newbies out. Further you gave me a platform and community I and my project can exist in.

For huge code contributions I want to thank [blindndangerous](https://github.com/blindndangerous) who did a lot of work on this project as well. Over the project lifespan I think I got like 50 PRs and more from him regarding all types of problems from small annoying stuff to work out up to bigger UI suggestions and accessibility for whole screens of the game.
Further thanks to Ahix who created [refactoring prompts for large AI-coded projects](https://github.com/ahicks92/llm-mod-refactoring-prompts) I ran on top of my own refactorings to ensure code quality and maintainability.

For testing the betas, feedback and ideas I want to thank:
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

For sighted testing to understand visual workflows and confirming some things I want to thank:
- https://github.com/mauriceKA
- VeganWolf
- Lea Holstein

<h3>Tools used</h3>

- Claude with all included models
- MelonLoader
- Harmony for IL patching
- Tolk for screen reader communication
- ILSpy for decompiling game code

Support your modder:
Creating this mod was not just a lot of fun and empowerment for me but cost me really a lot of time and real money cause I used the 100 Euro Claude plan. Further to maintain the mod I will have to keep at least on the 20 Euro plan and most probably will stay on the bigger one to work on new projects. And I bought some in-game real money stuff for testing but would have done this anyways for the most part.
So if you are willing and able to afford some financial reward for my investments you can look over here. I would be very grateful if this project brings me not just fulfillment but even some money.
links todo

<h2>License</h2>

This project is licensed under the GNU General Public License v3.0. See the LICENSE file for details.

<h2>Links</h2>

- GitHub: https://github.com/JeanStiletto/AccessibleArena
- MelonLoader: https://github.com/LavaGang/MelonLoader
- MTG Arena: https://magic.wizards.com/mtgarena
- Accessibility modding Discord: link todo

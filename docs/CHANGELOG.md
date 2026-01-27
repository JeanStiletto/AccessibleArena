# Changelog

All notable changes to Accessible Arena.

## v0.2.6 - 2026-01-27

### Bug Fixes
- Fix confirmation popups not navigable in Settings menu
  - Popups like logout confirmation now properly detected and announced
  - Popup message is read aloud (e.g., "Confirmation. Are you sure you want to log out?")
  - Popup buttons (OK/Cancel) navigable with arrow keys
  - Enter activates selected button
  - Backspace dismisses popup (finds cancel/close button)
  - After popup closes, navigation returns to Settings menu

### Technical
- SettingsMenuNavigator now subscribes to PanelStateManager.OnPanelChanged
- Added popup tracking state (_activePopup, _isPopupActive)
- DiscoverPopupElements() finds SystemMessageButtonView, CustomButton, and Button components
- ExtractPopupMessage() reads popup title/message for announcement
- DismissPopup() and FindPopupCancelButton() handle backspace dismissal

**Files:** `SettingsMenuNavigator.cs`

## v0.2.5 - 2026-01-27

### Bug Fixes (Popup Button - Still Not Working)

Three fixes attempted for popup button activation issue. Popup buttons (e.g., "Continue editing" / "Discard deck") still require two Enter presses despite these changes:

1. **Popup destruction detection during scene change**
   - `AlphaPanelDetector.CleanupDestroyedPanels()` now reports popups as closed when their GameObject is destroyed
   - Previously, destroyed popups were silently removed from tracking without notifying PanelStateManager
   - Uses `ReportPanelClosedByName()` since GameObject reference is null

2. **Enter key consumption in menu navigation**
   - `GeneralMenuNavigator` now uses `InputManager.GetEnterAndConsume()` instead of `Input.GetKeyDown()`
   - Prevents game from processing Enter on EventSystem's selected object (e.g., Nav_Decks) simultaneously
   - Enter key is marked as consumed so Harmony patch blocks it from game's KeyboardManager

3. **SystemMessageButtonView method name fix**
   - Changed from `OnClick()` to `Click()` - the actual method name on SystemMessageButtonView
   - Debug logging revealed available methods: `Init(SystemMessageButtonData, Action)` and `Click()`
   - `TryInvokeMethod` now tries `Click()` first, then `OnClick()`, then `OnButtonClicked()`

**Files:** `AlphaPanelDetector.cs`, `GeneralMenuNavigator.cs`, `UIActivator.cs`

## v0.2.4 - 2026-01-27

### New Features
- Add deck builder collection card accessibility
  - Collection cards now appear in navigable "Collection" group
  - Left/Right arrows navigate between cards
  - Up/Down arrows read card details (name, type, mana cost, rules text, etc.)
  - CardInfoNavigator automatically activated when focusing on cards

### Bug Fixes
- Fix DeckBuilderCollection group not appearing in group list
  - Added DeckBuilderCollection to groupOrder in GroupedNavigator
- Fix CardInfoNavigator not prepared for collection cards
  - Added UpdateCardNavigationForGroupedElement() helper method
  - Called after grouped navigation moves to prepare card reading

### Technical
- Changed UpdateCardNavigation() from private to protected in BaseNavigator
- Added card navigation integration to grouped navigation methods (MoveNext, MovePrevious, MoveFirst, MoveLast, HandleGroupedEnter)

## v0.2.3 - 2026-01-25

### New Features
- Add Settings menu accessibility during duels
  - Press Escape to open Settings menu in any scene (menus, duels, drafts, sealed)
  - New dedicated SettingsMenuNavigator handles all Settings navigation
  - Settings code removed from GeneralMenuNavigator for cleaner separation

### Architecture
- New overlay navigator integration pattern
  - Higher-priority navigators take control when overlays appear
  - Lower-priority navigators (DuelNavigator, GeneralMenuNavigator) yield via ValidateElements()
  - Uses Harmony-based PanelStateManager.IsSettingsMenuOpen for precise timing
  - Pattern documented in BEST_PRACTICES.md for future similar integrations

## v0.2.2 - 2026-01-25

### Bug Fixes
- Fix false "countered" announcements for resolving spells
  - Instants/sorceries going to graveyard after normal resolution no longer announced as "countered"
  - Only actual counterspells trigger "was countered" announcement
  - Added "countered and exiled" for exile-on-counter effects (Dissipate, etc.)

### Improvements
- Distinguish triggered/activated abilities from cast spells on stack
  - Abilities now announced as "[Name] triggered, [rules]" instead of "Cast [Name]"
  - Spells still announced as "Cast [Name], [P/T], [rules]"
- Enhanced attacker announcements when leaving Declare Attackers phase
  - Now announces each attacker with name and P/T
  - Example: "2 attackers. Sheoldred 4/5. Graveyard Trespasser 3/4. Declare blockers"

## v0.2.1 - 2026-01-25

### Bug Fixes
- Fix combat state display during Declare Attackers/Blockers phases
  - Creatures no longer incorrectly show as "attacking" or "blocking" before being assigned
  - Game pre-creates IsBlocking on all potential blockers (inactive) - now correctly checks active state
  - Display checks active state while internal counting checks existence (for token compatibility)

### Improvements
- Add "can attack" state during Declare Attackers phase (matches "can block" pattern)
- Combat states now correctly show:
  - Attackers: "can attack" → "attacking"
  - Blockers: "can block" → "selected to block" → "blocking"
- Remove unused debug field

## v0.2 - 2026-01-24

### Bug Fixes
- Fix attacker and blocker counting for tokens
  - Tokens with inactive visual indicators are now correctly counted
  - Previously "5 attackers" could be announced as "2 attackers"
- Fix life announcement ownership ("you gained" vs "opponent gained")
- Remove redundant ownership from token creation announcements
- Fix backspace navigation in content panels (BoosterChamber, Profile, Store, etc.)
- Fix rescan not triggering when popups close
- Fix popup detection and double announcements

### New Features
- Add T key to announce current turn and phase
- Add vault progress display in pack openings
  - Shows "Vault Progress +99" instead of "Unknown card" for duplicate protection
- Add debug tools: F11 (card details), F12 (UI hierarchy) - documented in help menu

### Improvements
- Major panel detection overhaul with unified alpha-based system
- New PanelStateManager for centralized panel state tracking
- Simplified GeneralMenuNavigator: removed plugin architecture, extracted debug helpers
- Improved PlayBlade detection and Color Challenge navigation
- Improved button activation reliability

### Documentation
- Updated debug keys in help menu
- Documented architecture improvements
- Updated known issues

## v0.1.4.1 - 2026-01-21

- Fix NPE rewards "Take Reward" button not appearing in navigation

## v0.1.3 - 2026-01-21

- Add BoosterOpenNavigator for pack opening accessibility
- Fix blocker tracking reset during multiple blocker assignments
- Fix arrow keys navigating menu buttons in empty zones

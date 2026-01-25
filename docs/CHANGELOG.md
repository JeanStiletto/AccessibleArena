# Changelog

All notable changes to Accessible Arena.

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

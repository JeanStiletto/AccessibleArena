# Changelog

All notable changes to Accessible Arena.

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

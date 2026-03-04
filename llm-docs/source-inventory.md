# Source File Inventory

Generated: 2026-03-04

Total files: 91 (excluding obj/ and bin/)
Total lines: ~55,635

## Root (src/)

| File | Lines | Description |
|------|------:|-------------|
| AccessibleArenaMod.cs | 309 | MelonMod entry point; initializes all services, navigators, and wires up the update loop |
| ScreenReaderOutput.cs | 86 | P/Invoke wrapper for Tolk.dll; sends speech output to NVDA screen reader |

## Core/Interfaces

| File | Lines | Description |
|------|------:|-------------|
| IScreenNavigator.cs | 43 | Interface for screen-specific navigators with priority, activation, and element management |
| IAnnouncementService.cs | 15 | Interface for screen reader announcement service (normal, interrupt, verbose, repeat) |
| IShortcutRegistry.cs | 15 | Interface for registering and processing keyboard shortcuts with modifier keys |
| IInputHandler.cs | 14 | Interface for input handling with key press, navigation, accept, and cancel events |

## Core/Models

| File | Lines | Description |
|------|------:|-------------|
| Strings.cs | 1088 | Centralized localized string constants for all user-facing announcements |
| TargetInfo.cs | 62 | Data model for targeting information (GameObject, name, instance ID, target type) |
| ShortcutDefinition.cs | 34 | Model for a keyboard shortcut (key, modifier, action, description) |
| AnnouncementPriority.cs | 10 | Enum for announcement priority levels (Low, Normal, High, Immediate) |

## Core/Services

| File | Lines | Description |
|------|------:|-------------|
| GeneralMenuNavigator.cs | 4766 | General-purpose navigator for menu screens using CustomButton components; fallback for unhandled screens |
| CardModelProvider.cs | 4626 | Reflection-based access to card model data (names, types, costs, rules text, categorization) |
| BaseNavigator.cs | 2928 | Abstract base class for all screen navigators; handles Tab/Enter navigation, element management, announcements |
| DuelAnnouncer.cs | 2294 | Announces duel events to screen reader (draws, plays, damage, phase changes, combat) |
| UIActivator.cs | 2196 | Centralized UI activation utilities (clicking buttons, toggling checkboxes, playing cards) |
| BrowserNavigator.cs | 2177 | Navigator for browser UIs in duel scene (scry, surveil, London mulligan) |
| StoreNavigator.cs | 2042 | Standalone navigator for the MTGA Store screen with product browsing and purchasing |
| UITextExtractor.cs | 1976 | Utility to extract readable text from Unity UI GameObjects across various component types |
| UIElementClassifier.cs | 1677 | Classifies UI elements by role and determines navigability for screen reader labeling |
| MasteryNavigator.cs | 1489 | Navigator for the Mastery/Rewards track screen (RewardTrack scene) |
| MenuDebugHelper.cs | 1288 | Static helper with verbose debug/logging methods extracted from GeneralMenuNavigator |
| PlayerPortraitNavigator.cs | 1197 | Navigator for player portrait/timer interactions during duels (V key zone, emotes) |
| WebBrowserAccessibility.cs | 1236 | Keyboard navigation and screen reader support for embedded Chromium browser popups (ZFBrowser) |
| CodexNavigator.cs | 1076 | Navigator for Codex of the Multiverse (Learn to Play) with TOC, content, and credits modes |
| HotHighlightNavigator.cs | 1053 | Unified navigator for highlight-based card selection (targeting, discard, abilities) |
| BrowserZoneNavigator.cs | 1040 | Zone-based navigation within browser UIs (top/bottom piles in scry/surveil) |
| BoosterOpenNavigator.cs | 999 | Navigator for the booster pack card list after opening a pack |
| DeckInfoProvider.cs | 942 | Reflection-based access to deck statistics (card count, mana curve, type breakdown) |
| EventAccessor.cs | 837 | Reflection-based access to event tiles, event pages, and packet selection data |
| DuelNavigator.cs | 823 | Top-level navigator for the duel/gameplay scene; coordinates sub-navigators |
| CardDetector.cs | 817 | Static utility for detecting card GameObjects and basic card operations |
| LoadingScreenNavigator.cs | 806 | Navigator for transitional screens (match end, matchmaking queue, game loading) |
| BrowserDetector.cs | 776 | Detects active browser type and state (scry, surveil, London mulligan, workflow) |
| RewardPopupNavigator.cs | 720 | Navigator for reward popups from mail claims, store purchases, etc. |
| BattlefieldNavigator.cs | 700 | Battlefield navigation organized into 6 rows by card type and ownership |
| CombatNavigator.cs | 637 | Combat phase navigation (declare attackers, declare blockers, confirmations) |
| AdvancedFiltersNavigator.cs | 625 | Grid-based navigator for the Advanced Filters popup in Collection/Deck Builder |
| UIFocusTracker.cs | 579 | Polls EventSystem to track UI focus changes and announce them via screen reader |
| SettingsMenuNavigator.cs | 576 | Dedicated navigator for the in-game Settings menu (works in all scenes) |
| NPERewardNavigator.cs | 564 | Navigator for New Player Experience reward screen showing unlocked cards |
| ManaColorPickerNavigator.cs | 541 | Navigator for the mana color selector popup (any-color mana sources) |
| FriendInfoProvider.cs | 495 | Reads friend tile info (display name, status, actions) for accessibility navigation |
| DraftNavigator.cs | 478 | Navigator for the draft card picking screen (DraftContentController) |
| UnifiedPanelDetector.cs | 458 | Unified panel detection using CanvasGroup alpha state comparison |
| InputFieldEditHelper.cs | 440 | Shared input field editing logic (edit mode, key navigation, character announcements) |
| DropdownStateManager.cs | 418 | Unified dropdown state management; single source of truth for dropdown mode tracking |
| PanelAnimationDiagnostic.cs | 405 | Diagnostic tool for tracking panel animation states vs alpha changes |
| PriorityController.cs | 404 | Reflection wrapper for full control toggle and phase stop toggle functionality |
| AssetPrepNavigator.cs | 339 | Navigator for the AssetPrep (download) screen on fresh install |
| LocaleManager.cs | 339 | Singleton that loads and resolves localized strings from JSON files with fallback chain |
| ModSettingsNavigator.cs | 350 | Modal navigator for mod settings (F2 menu, toggle settings, language selection) |
| MenuScreenDetector.cs | 365 | Detects active content controllers and screens in the MTGA menu system |
| OverlayNavigator.cs | 365 | Handles modal overlays (What's New carousel, announcements, reward popups) |
| MenuPanelTracker.cs | 331 | Tracks active menu panels and provides content controller detection |
| CardPoolAccessor.cs | 329 | Reflection-based access to CardPoolHolder API for collection page navigation |
| RecentPlayAccessor.cs | 327 | Reflection-based access to LastPlayedBladeContentView for Recent tab deck labels |
| HelpNavigator.cs | 251 | Modal help menu navigator (F1, Up/Down navigation through keybind help items) |
| CardInfoNavigator.cs | 237 | Vertical navigation through card info blocks (name, cost, type, P/T, rules, etc.) |
| InputManager.cs | 238 | Input manager with key consumption to block keys from reaching the game's KeyboardManager |
| ModSettings.cs | 220 | Mod settings with JSON file persistence (UserData/AccessibleArena.json) |
| DropdownEditHelper.cs | 207 | Shared dropdown editing logic wrapping BaseNavigator's static dropdown methods |
| ExtendedInfoNavigator.cs | 167 | Modal navigator for extended card info (keyword descriptions, linked faces) |
| PreBattleNavigator.cs | 162 | Navigator for the pre-game VS screen (Continue to battle / Cancel prompt) |
| NavigatorManager.cs | 140 | Manages all screen navigators; handles priority, activation, and lifecycle |
| ZoneNavigator.cs | 1006 | Duel zone navigation (hand, graveyard, exile, stack, command zone) with priority ownership |
| DebugConfig.cs | 68 | Centralized debug configuration toggle for all mod logging |
| AnnouncementService.cs | 58 | Implementation of IAnnouncementService; routes messages to ScreenReaderOutput |
| ShortcutRegistry.cs | 53 | Implementation of IShortcutRegistry; manages shortcut registration and key processing |
| DuelHolderCache.cs | 41 | Shared static cache for duel card holder GameObjects to avoid repeated scene scans |

## Core/Services/ElementGrouping

| File | Lines | Description |
|------|------:|-------------|
| GroupedNavigator.cs | 1805 | Hierarchical group-based navigation with levels (groups, items within group) |
| ChallengeNavigationHelper.cs | 753 | Helper for Challenge screen navigation (Direct Challenge, deck selection) |
| ElementGroupAssigner.cs | 633 | Assigns UI elements to groups based on parent hierarchy and name patterns |
| OverlayDetector.cs | 363 | Simplified overlay detection; determines which overlay should suppress other groups |
| ElementGroup.cs | 247 | Enum defining UI element group categories for menu navigation |
| PlayBladeNavigationHelper.cs | 230 | Helper for Play blade navigation result handling and deck selection flow |

## Core/Services/PanelDetection

| File | Lines | Description |
|------|------:|-------------|
| PanelStateManager.cs | 579 | Single source of truth for panel state; detectors report changes, consumers subscribe to events |
| ReflectionPanelDetector.cs | 290 | Polls IsOpen properties on menu controllers via reflection for panel detection |
| AlphaPanelDetector.cs | 277 | Uses CanvasGroup alpha to detect popup visibility (system messages, dialogs, modals) |
| HarmonyPanelDetector.cs | 268 | Uses Harmony patches to detect panel state changes (PlayBlade, Settings, Blades, Social) |
| PanelInfo.cs | 223 | Information about an active panel with canonical naming, behavior flags, and classification utilities |
| PanelType.cs | 39 | Enum for MTGA panel types (Login, Settings, Popup, Blade, Social, etc.) |

## Core/Services/PanelDetection/old/detector-plugin-system

| File | Lines | Description |
|------|------:|-------------|
| PanelRegistry.cs | 228 | (Old) Centralized knowledge about MTGA panels and detection method assignment |
| PanelDetectorManager.cs | 110 | (Old) Manager coordinating all panel detectors and their update cycles |
| IPanelDetector.cs | 35 | (Old) Interface for panel detection plugins reporting to PanelStateManager |

## Core/Services/old

| File | Lines | Description |
|------|------:|-------------|
| EventTriggerNavigator.cs | 643 | (Old) Navigator for screens using EventTrigger/CustomButton components (NPE, rewards, packs) |
| HighlightNavigator.cs | 535 | (Old) Tab navigation through highlighted/playable cards; replaced by HotHighlightNavigator |
| TargetNavigator.cs | 479 | (Old) Target selection during spells/abilities; replaced by HotHighlightNavigator |
| DiscardNavigator.cs | 274 | (Old) Card selection during discard phases; replaced by HotHighlightNavigator |
| CodeOfConductNavigator.cs | 220 | (Old) Navigator for terms/consent screens with checkboxes |
| LoginPanelNavigator.cs | 133 | (Old) Navigator for the Login panel (email/password entry) |
| WelcomeGateNavigator.cs | 43 | (Old) Navigator for the WelcomeGate login/register choice screen |

## Patches

| File | Lines | Description |
|------|------:|-------------|
| PanelStatePatch.cs | 1275 | Harmony patches intercepting panel state changes from game controllers (open/close events) |
| KeyboardManagerPatch.cs | 175 | Harmony patch for MTGA KeyboardManager to block keys in specific contexts (Enter in duels, etc.) |
| UXEventQueuePatch.cs | 167 | Harmony patch intercepting game events from UXEventQueue for screen reader announcements |
| EventSystemPatch.cs | 102 | Harmony patches blocking Enter on toggles and arrow keys during input field editing |
